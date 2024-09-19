import argparse
import cv2
import os
import imagehash
from PIL import Image
import numpy as np
import dlib
from scipy.spatial import distance as dist

# Initialize dlib face detector and predictor
detector = dlib.get_frontal_face_detector()
predictor = dlib.shape_predictor("assets/face_predictor.dat")

MIN_FACE_WIDTH, MIN_FACE_HEIGHT = 80, 80  # Face pixels threshold 80x80

def load_image(image_path):
    image = cv2.imread(image_path)
    if image is None:
        raise FileNotFoundError(f"Image not found at {image_path}")
    return image

def load_yolo_model():
    try:
        yolo_net = cv2.dnn.readNet('assets/yolov3.weights', 'assets/yolov3.cfg')
        layer_names = yolo_net.getLayerNames()
        unconnected_out_layers = yolo_net.getUnconnectedOutLayers()
        output_layers = [layer_names[i - 1] for i in unconnected_out_layers.flatten()]
        return yolo_net, output_layers
    except Exception as e:
        raise

def detect_objects(image, yolo_net, output_layers):
    height, width, _ = image.shape
    blob = cv2.dnn.blobFromImage(image, 0.00392, (416, 416), (0, 0, 0), True, crop=False)
    yolo_net.setInput(blob)
    outs = yolo_net.forward(output_layers)

    class_ids, confidences, boxes = [], [], []

    for out in outs:
        for detection in out:
            if detection.ndim == 1:
                detection = detection.reshape(1, -1)
            for obj in detection:
                scores = obj[5:]
                class_id = np.argmax(scores)
                confidence = scores[class_id]
                if confidence > 0.5:
                    center_x = int(obj[0] * width)
                    center_y = int(obj[1] * height)
                    w = int(obj[2] * width)
                    h = int(obj[3] * height)
                    x = int(center_x - w / 2)
                    y = int(center_y - h / 2)
                    boxes.append([x, y, w, h])
                    confidences.append(float(confidence))
                    class_ids.append(class_id)

    indices = cv2.dnn.NMSBoxes(boxes, confidences, 0.5, 0.4)
    objects = [boxes[i] for i in indices.flatten()] if len(indices) > 0 else []

    cropped_objects = [crop_object(image, *box) for box in objects]
    return cropped_objects

def crop_object(image, x, y, w, h):
    return image[y:y+h, x:x+w]

def get_blur(image):
    gray_image = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    blurr = cv2.Laplacian(gray_image, cv2.CV_64F).var()
    return blurr

def detect_main_focus(image):
    saliency = cv2.saliency.StaticSaliencySpectralResidual_create()
    (success, saliency_map) = saliency.computeSaliency(image)
    
    if not success:
        return None
    
    saliency_map = (saliency_map * 255).astype("uint8")
    _, thresh_map = cv2.threshold(saliency_map, 0, 255, cv2.THRESH_BINARY | cv2.THRESH_OTSU)
    contours, _ = cv2.findContours(thresh_map, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    
    if contours:
        largest_contour = max(contours, key=cv2.contourArea)
        x, y, w, h = cv2.boundingRect(largest_contour)
        main_focus = image[y:y+h, x:x+w]
        
        blur = get_blur(main_focus)
        if blur >= 40:
            return main_focus
    
    return None

def quality(image):
    gray_image = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    blurriness = cv2.Laplacian(gray_image, cv2.CV_64F).var()
    exposure = gray_image.mean()
    noise = gray_image.std()
    
    return blurriness, exposure, noise

def get_criteria(reference_image, exposure_min, exposure_max):
    reference_image = load_image(reference_image)
    blurriness, exposure, noise = quality(reference_image)
    
    criteria = {
        'blurriness_max': blurriness,
        'exposure_min': exposure * exposure_min,
        'exposure_max': exposure * exposure_max,
        'noise_max': noise * 4,
    }
    
    return criteria 

def hash_image(image_path):
    hash_value = imagehash.phash(Image.open(image_path))
    return hash_value

def detect_duplicates(image_path, hashes, threshold):
    image_hash = hash_image(image_path)
    is_duplicate = any(
        isinstance(existing_hash, imagehash.ImageHash) and
        (image_hash - existing_hash) < threshold
        for existing_hash in hashes.values()
    )
    return is_duplicate

LEFT_EYE_POINTS = list(range(36, 42))
RIGHT_EYE_POINTS = list(range(42, 48))

def resize_image(image, width=2000):
    aspect_ratio = width / float(image.shape[1])
    height = int(image.shape[0] * aspect_ratio)
    resized_image = cv2.resize(image, (width, height), interpolation=cv2.INTER_AREA)
    return resized_image

def calculate_ear(eye):
    A = dist.euclidean(eye[1], eye[5])
    B = dist.euclidean(eye[2], eye[4])
    C = dist.euclidean(eye[0], eye[3])
    return (A + B) / (2.0 * C)

def are_eyes_closed(image, ear_threshold=0.2):
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    faces = detector(gray)

    if not faces:
        return False

    for face in faces:
        if face.width() < MIN_FACE_WIDTH or face.height() < MIN_FACE_HEIGHT:
            continue

        landmarks = predictor(gray, face)

        left_eye = [(landmarks.part(point).x, landmarks.part(point).y) for point in LEFT_EYE_POINTS]
        right_eye = [(landmarks.part(point).x, landmarks.part(point).y) for point in RIGHT_EYE_POINTS]

        left_ear = calculate_ear(left_eye)
        right_ear = calculate_ear(right_eye)
        
        avg_ear = (left_ear + right_ear) / 2.0

        if avg_ear < ear_threshold:
            return True

    return False

def process_images(directory_path, reference_path, exposure_min, exposure_max):
    hashes = {}
    yolo_net, output_layers = load_yolo_model()
    
    try:
        criteria = get_criteria(reference_path, exposure_min, exposure_max)
    except FileNotFoundError as e:
        raise

    good_files, bad_files, duplicate_files, flagged_files = [], [], [], []
    reasons = []  # To store debugging information
    
    for filename in os.listdir(directory_path):
        if filename.lower().endswith(('.jpg', '.jpeg', '.png')):
            image_path = os.path.join(directory_path, filename)
            
            try:
                image = load_image(image_path)
                resized_image = resize_image(image)
                
                # Check if eyes are closed
                if are_eyes_closed(resized_image, ear_threshold=0.15):
                    flagged_files.append(filename)
                    reasons.append(f"{filename}: Eyes closed")
                    continue
                
                # Check for duplicates
                if detect_duplicates(image_path, hashes, threshold=17):
                    duplicate_files.append(filename)
                    reasons.append(f"{filename}: Duplicate detected")
                    continue
                
                # Check for blurriness and focus
                blur = get_blur(image)
                if blur <= criteria['blurriness_max']:
                    main_focus = detect_main_focus(resized_image)
                    if main_focus is not None:
                        objects = detect_objects(main_focus, yolo_net, output_layers)
                        if objects:
                            good_files.append(filename)
                            hashes[filename] = hash_image(image_path)
                            reasons.append(f"{filename}: Good quality (focus detected and objects found)")
                        else:
                            bad_files.append(filename)
                            reasons.append(f"{filename}: Bad quality (focus detected but no objects found)")
                    else:
                        bad_files.append(filename)
                        reasons.append(f"{filename}: Bad quality (no focus detected)")
                    continue
                
                blurriness, exposure, noise = quality(image)
                if (blurriness >= criteria['blurriness_max'] and 
                    criteria['exposure_min'] < exposure < criteria['exposure_max'] and 
                    noise < criteria['noise_max']):
                    good_files.append(filename)
                    hashes[filename] = hash_image(image_path)
                    reasons.append(f"{filename}: Good quality (meets criteria)")
                else:
                    reason = f"{filename}: Bad quality ("
                    if blurriness <= criteria['blurriness_max']:
                        reason += "blurriness threshold met, "
                    if criteria['exposure_min'] < exposure < criteria['exposure_max']:
                        reason += "exposure threshold met, "
                    if noise < criteria['noise_max']:
                        reason += "noise threshold met, "
                    reason += ")"
                    bad_files.append(filename)
                    reasons.append(reason)
                
            except Exception as e:
                reasons.append(f"{filename}: Error - {e}")

    # Create bash script
    bash_script = "mkdir \"Good\"\nmkdir \"Bad\"\nmkdir \"Duplicate\"\nmkdir \"Flagged\"\n"
    
    for filename in good_files:
        bash_script += f"move \"{filename}\" Good/\n"
    
    for filename in bad_files:
        bash_script += f"move \"{filename}\" Bad/\n"
        
    for filename in duplicate_files:
        bash_script += f"move \"{filename}\" Duplicate/\n"
            
    for filename in flagged_files:
        bash_script += f"move \"{filename}\" Flagged/\n"
    
    return bash_script,reasons

def main():
    parser = argparse.ArgumentParser(description="Process and classify images.")
    parser.add_argument("--directory", help="Directory containing images to process")
    parser.add_argument("--reference", help="Path to the reference image for criteria")
    parser.add_argument("--exposure_min", type=float, default=0.6, help="Minimum exposure multiplier")
    parser.add_argument("--exposure_max", type=float, default=1.5, help="Maximum exposure multiplier")
    
    args = parser.parse_args()

    try:
        bash_script, reasons = process_images(args.directory, args.reference, args.exposure_min, args.exposure_max)
        print(bash_script)
        
        # Print reasons for debugging
        #print("\nDebugging Information:")
        #for reason in reasons:
       #     print(reason)
        
    except Exception as e:
        print(f"An error occurred: {e}")

if __name__ == "__main__":
    main()
