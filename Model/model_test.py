import torch
from torch.utils.data import DataLoader
from torchvision import transforms

import utils
from model import LightEstimator
from mp_dataset import MPDataSet
import torch.nn as nn
import torch.nn.functional as F
import matplotlib.pyplot as plt

def main():
    device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
    # device = torch.device('cpu')
    print(device)
    dataset = MPDataSet('./data/room', transforms.Resize((512, 640)))
    dataloader = DataLoader(dataset, batch_size=1, shuffle=True)
    model = torch.load('model/model.pth', weights_only=False)
    model.to(device)
    loss_fn = nn.MSELoss()
    model.eval()
    num_batches = len(dataloader)
    loss = 0.0
    batch = 0

    fig, axes = plt.subplots(1, 3)

    with torch.no_grad():
        for data in dataloader:
            batch += 1
            color_img, skybox_img = data[0].to(device), data[2].to(device)
            est_sh = model(color_img)
            sh = utils.cubemap2sh(skybox_img)
            loss += loss_fn(est_sh, sh).item()
            print(sh, est_sh)
            print(loss/batch)
    print(loss/num_batches)


if __name__ == '__main__':
    main()