import torch.optim
import torch.nn as nn
import torch.nn.functional as F
import matplotlib.pyplot as plt
import utils
from torch.utils.data import DataLoader
from torchvision import transforms

from model import LightEstimator
from mp_dataset import MPDataSet


def main():
    device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
    #device = torch.device('cpu')
    print(device)
    dataset = MPDataSet('./data/room', transforms.Resize((512, 640)))
    dataloader = DataLoader(dataset, batch_size=16, shuffle=True)
    model = LightEstimator()
    print(sum(p.numel() for p in model.parameters()))
    model.to(device)
    loss_fn = nn.MSELoss()
    optimizer = torch.optim.SGD(model.parameters(), lr=1e-3)
    for epoch in range(10):
        running_loss = 0.0
        for batch, data in enumerate(dataloader, 0):
            color_img, skybox_img = data[0].to(device), data[2].to(device)
            optimizer.zero_grad()
            sh = utils.cubemap2sh(skybox_img)
            est_sh = model(color_img)
            loss = loss_fn(est_sh, sh)
            loss.backward()
            optimizer.step()
            running_loss += loss.item()
            if batch % 10 == 9:
                #print(sh)
                print(f'epoch: {epoch} batch: {batch} loss: {running_loss/10.0}')
                running_loss = 0.0
    torch.save(model, 'model/model.pth')


if __name__ == '__main__':
    main()