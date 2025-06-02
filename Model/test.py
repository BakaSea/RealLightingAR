import torch
from torch.utils.data import DataLoader
from torchvision.transforms import transforms

import utils
from mp_dataset import MPDataSet
import matplotlib.pyplot as plt

def main():
    dataset = MPDataSet('./data/room')
    dataloader = DataLoader(dataset, batch_size=4, shuffle=True)
    _, _, cubemap = dataset[0]
    #cubemap[[0, 2, 3, 4, 5]] = torch.ones_like(cubemap[0])

    cubemap = cubemap.unsqueeze(0)

    one = torch.ones(size=(1, 6, 3, 1024, 1024))

    # 2. 调用你写的函数
    sh = utils.cubemap2sh(cubemap)  # [1,9,3]

    # 3. 输出
    print("SH coeffs for constant=1 env:")
    #for batch in range(4):
    for i in range(27):
        print(f"l={i} → {sh[0, i, 0].cpu().tolist()}")

if __name__ == '__main__':
    main()