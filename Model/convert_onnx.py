import torch
from model import LightEstimator

model = torch.load('model/model.pth', weights_only=False)
model.to(torch.device('cpu'))
torch.onnx.export(model, torch.randn(1, 3, 512, 640), 'model/model.onnx', dynamo=True)
