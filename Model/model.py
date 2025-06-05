import torch
import torch.nn as nn
import torch.nn.functional as F

class DoubleConv2d(nn.Module):

    def __init__(self, in_channels, out_channels, kernel_size):
        super().__init__()
        self.double_conv2d = nn.Sequential(
            nn.Conv2d(in_channels, out_channels, kernel_size, padding=1),
            nn.BatchNorm2d(out_channels),
            nn.ReLU(inplace=True),
            nn.Conv2d(out_channels, out_channels, kernel_size, padding=1),
            nn.BatchNorm2d(out_channels),
            nn.ReLU(inplace=True)
        )

    def forward(self, x):
        return self.double_conv2d(x)

class Down(nn.Module):

    def __init__(self, in_channels, out_channels, kernel_size, stride=2, dilation=1):
        super().__init__()
        self.down = nn.Sequential(
            nn.MaxPool2d(kernel_size, padding=1, stride=stride, dilation=dilation),
            DoubleConv2d(in_channels, out_channels, kernel_size)
        )

    def forward(self, x):
        return self.down(x)

class Up(nn.Module):

    def __init__(self, in_channels, out_channels, kernel_size, stride=2, dilation=1):
        super().__init__()
        self.up = nn.ConvTranspose2d(in_channels, out_channels, kernel_size, padding=1, output_padding=1, stride=stride, dilation=dilation)
        self.conv2d = DoubleConv2d(out_channels*2, out_channels, kernel_size)

    def forward(self, x, skip_feature):
        y = self.up(x)
        #z = F.interpolate(skip_feature, size=y.shape[2:], mode='bilinear', align_corners=False)
        return self.conv2d(torch.cat([y, skip_feature], dim=1))

class Encoder(nn. Module):

    def __init__(self):
        super().__init__()
        self.encoder1 = DoubleConv2d(3, 32, 3)
        self.encoder2 = Down(32, 64, 3, 2, 1)
        self.encoder3 = Down(64, 128, 3, 2)
        self.encoder4 = Down(128, 256, 3, 2)
        self.encoder5 = Down(256, 256, 3, 2)
        self.encoder6 = Down(256, 512, 3, 2)

    def forward(self, x):
        x1 = self.encoder1(x)
        x2 = self.encoder2(x1)
        x3 = self.encoder3(x2)
        x4 = self.encoder4(x3)
        x5 = self.encoder5(x4)
        x6 = self.encoder6(x5)
        return x1, x2, x3, x4, x5, x6

class Decoder(nn.Module):

    def __init__(self, out_channels):
        super().__init__()
        self.decoder5 = Up(512, 256, 3, 2)
        self.decoder4 = Up(256, 256, 3, 2)
        self.decoder3 = Up(256, 128, 3, 2)
        self.decoder2 = Up(128, 64, 3, 2, 1)
        self.decoder1 = Up(64, 32, 3, 2, 1)
        self.out_conv2d = nn.Conv2d(32, out_channels, 3, padding=1, stride=1, dilation=1)

    def forward(self, x1, x2, x3, x4, x5, x6):
        y = self.decoder5(x6, x5)
        y = self.decoder4(y, x4)
        y = self.decoder3(y, x3)
        y = self.decoder2(y, x2)
        y = self.decoder1(y, x1)
        return self.out_conv2d(y)

class SHEstimator(nn.Module):

    def __init__(self):
        super().__init__()
        self.fc = nn.Sequential(
            Down(512, 512, 3, 2, 1),
            Down(512, 1024, 3, 2),
            nn.Flatten(),
            nn.Linear(1024*4*5, 4096),
            nn.ReLU(inplace=True),
            nn.Dropout(p=0.25),
            nn.Linear(4096, 27)
        )

    def forward(self, x):
        return self.fc(x)

class LightEstimator(nn.Module):

    def __init__(self):
        super().__init__()
        self.encoder = Encoder()
        #self.normal_decoder = Decoder(3)
        #self.albedo_decoder = Decoder(3)
        #self.roughness_decoder = Decoder(1)
        self.sh_estimator = SHEstimator()

    def forward(self, img):
        height, width = img.shape[2], img.shape[3]
        x1, x2, x3, x4, x5, x6 = self.encoder(img)
        #albedo = self.albedo_decoder(x1, x2, x3, x4, x5, x6)
        #normal = self.normal_decoder(x1, x2, x3, x4, x5, x6)
        #roughness = self.roughness_decoder(x1, x2, x3, x4, x5, x6)
        sh = self.sh_estimator(x6)
        return sh.view(-1, 27, 1)