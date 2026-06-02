from ultralytics import YOLO
import torch
import cv2


model=YOLO("./runs/detect/train/weights/yolov8n.pt")
path = model.export(format="onnx",imgsz=(640,640))

#img=cv2.imread("6a263cfde3381960d97287a3bc015860.jpg")

#with torch.no_grad():
   #results=model(source="6a263cfde3381960d97287a3bc015860.jpg",conf=0.2,imgsz=(800,800))
#results[0].show()
