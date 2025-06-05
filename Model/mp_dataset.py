import os

import torch
from torch.utils.data import Dataset
from torchvision.io import read_image
from torchvision.transforms.v2.functional import convert_image_dtype

COLOR_IMAGES = 'undistorted_color_images'
DEPTH_IMAGES = 'undistorted_depth_images'
NORMAL_IMAGES = 'undistorted_normal_images'
SKYBOX_IMAGES = 'matterport_skybox_images'

class MPDataSet(Dataset):

    def __init__(self, root_dir, transform=None):
        self.root_dir = root_dir
        self.transform = transform
        self.img_list = []
        for room in os.listdir(root_dir):
            for img in os.listdir(os.path.join(root_dir, room, COLOR_IMAGES)):
                if img.endswith('.jpg'):
                    self.img_list.append([room, img[0:-9], img[-7], img[-5]])

    def __len__(self):
        return len(self.img_list)

    def __getitem__(self, idx):
        room, iid, x, y = self.img_list[idx]
        color_img = read_image(os.path.join(self.root_dir, room, COLOR_IMAGES, iid+'_i'+x+'_'+y+'.jpg'))
        depth_img = read_image(os.path.join(self.root_dir, room,  DEPTH_IMAGES, iid+'_d'+x+'_'+y+'.png'))
        # normal_img_x = read_image(os.path.join(self.root_dir, NORMAL_IMAGES, iid+'_d'+x+'_'+y+'_nx.png'))
        # normal_img_y = read_image(os.path.join(self.root_dir, NORMAL_IMAGES, iid+'_d'+x+'_'+y+'_ny.png'))
        # normal_img_z = read_image(os.path.join(self.root_dir, NORMAL_IMAGES, iid+'_d'+x+'_'+y+'_nz.png'))
        # normal_img = torch.cat((normal_img_x, normal_img_y, normal_img_z), dim=0)
        skybox_img = []
        for i in range(6):
            skybox_img.append(convert_image_dtype(read_image(os.path.join(self.root_dir, room, SKYBOX_IMAGES, iid+'_skybox'+str(i)+'_sami.jpg'))))
        skybox_img = torch.stack(skybox_img)

        color_img = convert_image_dtype(color_img)
        depth_img = convert_image_dtype(depth_img)
        #normal_img = normal_img.float()/32768.0-1.0
        if self.transform:
            color_img = self.transform(color_img)
            depth_img = self.transform(depth_img)
            #normal_img = self.transform(normal_img)
        return color_img, depth_img, skybox_img
