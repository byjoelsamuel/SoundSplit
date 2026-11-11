# Renders SoundSplit/Assets/logo.png and SoundSplit.ico.  Usage: python3 tools/make_icon.py SoundSplit/Assets
# SoundSplit icon, based on the hand-drawn sketch: speaker -> three cables -> three app tiles.
from PIL import Image, ImageDraw
import sys, math

INK = (17, 24, 39, 255)
CYAN, RED, PURPLE = (0, 200, 255, 255), (229, 32, 32, 255), (139, 43, 160, 255)
BG = (255, 255, 255, 255)

def squircle(d, W, pad, n=5.0, fill=None, outline=None, width=0):
    r = W/2 - pad; c = W/2
    pts = []
    for i in range(720):
        t = 2*math.pi*i/720
        ct, st = math.cos(t), math.sin(t)
        x = c + r*math.copysign(abs(ct)**(2/n), ct)
        y = c + r*math.copysign(abs(st)**(2/n), st)
        pts.append((x, y))
    if fill: d.polygon(pts, fill=fill)
    if outline: d.line(pts + [pts[0]], fill=outline, width=width, joint="curve")

def bezier(p0, p1, p2, p3, n=48):
    out = []
    for i in range(n+1):
        t = i/n; u = 1-t
        out.append((u**3*p0[0]+3*u*u*t*p1[0]+3*u*t*t*p2[0]+t**3*p3[0],
                    u**3*p0[1]+3*u*u*t*p1[1]+3*u*t*t*p2[1]+t**3*p3[1]))
    return out

def render(size, small=False):
    SS = 8 if size <= 64 else 4
    W = size*SS
    u = W/24.0
    P = lambda x, y: (x*u, y*u)
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    border = (1.25 if small else 0.85)*u
    squircle(d, W, W*0.035, fill=INK)
    squircle(d, W, W*0.035 + border, fill=BG)

    # speaker: back plate, cone, front rim, dome
    d.rounded_rectangle([*P(4.6, 9.4), *P(6.9, 14.6)], radius=0.55*u, fill=INK)
    d.polygon([P(6.5, 9.6), P(9.9, 6.6), P(9.9, 17.4), P(6.5, 14.4)], fill=INK)
    d.rounded_rectangle([*P(9.5, 6.1), *P(10.9, 17.9)], radius=0.7*u, fill=INK)
    d.pieslice([*P(9.7, 10.2), *P(12.3, 13.8)], start=-90, end=90, fill=INK)

    # cables
    lw = (1.25 if small else 0.95)*u
    cables = [
        bezier(P(12.9, 11.0), P(14.0, 11.0), P(13.6, 6.9), P(15.4, 6.9), 200),
        [P(13.1, 12.0), P(15.6, 12.0)],
        bezier(P(12.9, 13.0), P(14.0, 13.0), P(13.6, 17.1), P(15.4, 17.1), 200),
    ]
    for pts in cables:
        # stamp round dabs densely along the path: smooth, evenly thick curve with round caps
        dense = []
        for a, b in zip(pts, pts[1:]):
            steps = max(2, int(math.dist(a, b) / (lw * 0.08)))
            dense += [(a[0] + (b[0]-a[0])*k/steps, a[1] + (b[1]-a[1])*k/steps) for k in range(steps)]
        dense.append(pts[-1])
        for p in dense:
            d.ellipse([p[0]-lw/2, p[1]-lw/2, p[0]+lw/2, p[1]+lw/2], fill=INK)

    # app tiles
    r = 0.9*u
    d.rounded_rectangle([*P(15.0, 5.0), *P(19.2, 8.8)], radius=r, fill=CYAN)
    d.rounded_rectangle([*P(15.2, 10.1), *P(19.4, 13.9)], radius=r, fill=RED)
    d.rounded_rectangle([*P(15.0, 15.2), *P(19.2, 19.0)], radius=r, fill=PURPLE)
    return img.resize((size, size), Image.LANCZOS)

out = sys.argv[1]
render(512).save(out + "/logo.png")
frames = [render(s, small=(s <= 32)) for s in (16, 20, 24, 32, 48, 64, 128)]
big = render(256)
big.save(out + "/SoundSplit.ico", sizes=[(f.width, f.height) for f in frames + [big]], append_images=frames)

# preview sheet: large, small sizes, on light and dark
sheet = Image.new("RGBA", (900, 330), (244, 245, 247, 255))
L = render(256); sheet.paste(L, (20, 37), L)
x = 300
for s in (128, 64, 32, 16):
    im = render(s, small=s <= 32); sheet.paste(im, (x, 40), im); x += s + 20
dark = Image.new("RGBA", (300, 150), (30, 31, 34, 255))
for i, s in enumerate((64, 32, 16)):
    im = render(s, small=s <= 32); dark.paste(im, (20 + i*90, 40), im)
sheet.paste(dark, (300, 175))
z = render(16, True).resize((96, 96), Image.NEAREST); sheet.paste(z, (640, 190), z)
if len(sys.argv) > 2: sheet.save(sys.argv[2])  # optional preview sheet
