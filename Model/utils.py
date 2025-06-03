import torch

def cubemap2sh(cubemap):
    B, F, C, H, W = cubemap.shape
    device = cubemap.device
    assert F == 6 and C == 3

    # pixel grid [-1, 1]
    u = (torch.arange(W, device=device) + 0.5) / W * 2 - 1
    v = (torch.arange(H, device=device) + 0.5) / H * 2 - 1
    u, v = torch.meshgrid(u, v, indexing='xy')

    # Generate direction maps [6, 3, H, W]
    dirs = torch.zeros((6, 3, H, W), device=device)

    dirs[0] = torch.stack([u, torch.ones_like(u), v], dim=0)  # +Y (top)`
    dirs[1] = torch.stack([u, -v, torch.ones_like(u)], dim=0)  # +Z (front)
    dirs[2] = torch.stack([torch.ones_like(u), -v, -u], dim=0)  # +X (right)
    dirs[3] = torch.stack([-u, -v, -torch.ones_like(u)], dim=0)  # -Z (back)
    dirs[4] = torch.stack([-torch.ones_like(u), -v, u], dim=0)  # -X (left)
    dirs[5] = torch.stack([u, -torch.ones_like(u), -v], dim=0)  # -Y (bottom)

    dirs = dirs / dirs.norm(dim=1, keepdim=True)  # [6, 3, H, W]

    x, y, z = dirs[:, 0], dirs[:, 1], dirs[:, 2]  # each [6, H, W]

    # SH basis [6, 9, H, W]
    Y = torch.stack([
        0.282095 * torch.ones_like(x),
        0.488603 * y,
        0.488603 * z,
        0.488603 * x,
        1.092548 * x * y,
        1.092548 * y * z,
        0.315392 * (3 * z ** 2 - 1),
        1.092548 * x * z,
        0.546274 * (x ** 2 - y ** 2),
    ], dim=1)

    # Y = torch.stack([
    #     torch.ones_like(x),
    #     y,
    #     z,
    #     x,
    #     x * y,
    #     y * z,
    #     (3 * z ** 2 - 1),
    #     x * z,
    #     (x ** 2 - y ** 2),
    # ], dim=1)

    # Solid angle [1, H, W]
    s_temp = 1+u*u+v*v
    sa = 4/(torch.sqrt(s_temp)*s_temp)
    sa_sum = sa.view(-1).sum()*6

    # Expand shapes for broadcasting
    sa = sa.view(1, 1, H, W)  # [1, 1, H, W]
    Y = Y.unsqueeze(0)  # [1, 6, 9, H, W]
    sa = sa.expand(B, -1, -1, -1)  # [B, 1, H, W]
    sh_coeffs = torch.zeros((B, 9, 3), device=device)

    # Multiply and sum
    for f in range(6):
        color = cubemap[:, f]  # [B, 3, H, W]
        basis = Y[:, f]  # [1, 9, H, W]
        prod = basis.unsqueeze(1) * color.unsqueeze(2) * sa.unsqueeze(1)  # [B, 3, 9, H, W]
        prod = prod.permute(0, 2, 1, 3, 4)  # [B, 9, 3, H, W]
        sh_coeffs += prod.view(B, 9, 3, -1).sum(dim=-1)

    sh_coeffs *= 4*torch.pi/sa_sum

    sh_coeffs *= torch.tensor([
        0.282095,
        0.488603*2/3,
        0.488603*2/3,
        0.488603*2/3,
        1.092548*1/4,
        1.092548*1/4,
        0.315392*1/4,
        1.092548*1/4,
        0.546274*1/4,
    ], device=device).unsqueeze(0).unsqueeze(2)

    sh_coeffs = sh_coeffs.view(B, 27, 1)

    return sh_coeffs  # [B, 9, 3]