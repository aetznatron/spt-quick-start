"""Original SPT quick-start icon - a rifle cartridge case (Patronenhuelse).

Motif: a single brass casing with tapered neck, shoulder, extractor groove and
primer, standing on a dark badge. Own geometry, no game artwork.
"""
import os

from PIL import Image, ImageDraw, ImageFilter

NAVY = (11, 17, 26)
EMBER = (40, 22, 16)
AMBER = (205, 163, 94)

BRASS_DARK = (118, 82, 40)
BRASS_MID = (196, 150, 84)
BRASS_LIGHT = (236, 203, 141)
MOUTH = (26, 18, 11)

# written next to the repo's icon/ folder, independent of the working directory
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "icon") + os.sep


def horizontal_gradient(size, stops):
    grad = Image.new("RGB", (size, 1))
    for x in range(size):
        t = x / (size - 1)
        for i in range(len(stops) - 1):
            p0, c0 = stops[i]
            p1, c1 = stops[i + 1]
            if p0 <= t <= p1:
                k = 0.0 if p1 == p0 else (t - p0) / (p1 - p0)
                grad.putpixel((x, 0), tuple(int(c0[j] + (c1[j] - c0[j]) * k) for j in range(3)))
                break
        else:
            grad.putpixel((x, 0), stops[-1][1])
    return grad.resize((size, size))


def build(size=1024, detailed=True):
    S = size
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    bg = Image.new("RGB", (1, S))
    for y in range(S):
        t = (y / (S - 1)) ** 1.8
        bg.putpixel((0, y), tuple(int(NAVY[i] + (EMBER[i] - NAVY[i]) * t) for i in range(3)))
    bg = bg.resize((S, S))

    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, S - 1, S - 1), radius=int(S * 0.20), fill=255)
    img.paste(bg, (0, 0), mask)

    draw = ImageDraw.Draw(img, "RGBA")

    inset = int(S * 0.058)
    draw.rounded_rectangle((inset, inset, S - inset, S - inset), radius=int(S * 0.15),
                           outline=AMBER + (90,), width=max(2, int(S * 0.011)))

    if detailed:
        glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        ImageDraw.Draw(glow).ellipse((int(S * 0.26), int(S * 0.10), int(S * 0.74), int(S * 0.92)),
                                     fill=AMBER + (34,))
        img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(int(S * 0.075))))

    if detailed:
        neck_x0, neck_x1 = 0.404, 0.596
        body_x0, body_x1 = 0.348, 0.652
        neck_y0, neck_y1 = 0.196, 0.286
        shoulder_y = 0.400
        base_y = 0.752
        rim_y1 = 0.830
        base_x0, base_x1 = 0.362, 0.638
    else:
        neck_x0, neck_x1 = 0.386, 0.614
        body_x0, body_x1 = 0.330, 0.670
        neck_y0, neck_y1 = 0.184, 0.290
        shoulder_y = 0.400
        base_y = 0.748
        rim_y1 = 0.836
        base_x0, base_x1 = 0.344, 0.656

    casing = Image.new("L", (S, S), 0)
    cd = ImageDraw.Draw(casing)

    def px(v):
        return int(S * v)

    cd.rectangle((px(neck_x0), px(neck_y0), px(neck_x1), px(neck_y1)), fill=255)
    cd.polygon([(px(neck_x0), px(neck_y1)), (px(neck_x1), px(neck_y1)),
                (px(body_x1), px(shoulder_y)), (px(base_x1), px(base_y)),
                (px(base_x0), px(base_y)), (px(body_x0), px(shoulder_y))], fill=255)

    rim_x0, rim_x1 = base_x0 - 0.014, base_x1 + 0.014
    rim_y0 = base_y
    cd.rounded_rectangle((px(rim_x0), px(rim_y0), px(rim_x1), px(rim_y1)),
                         radius=int(S * 0.010), fill=255)

    if detailed:
        gradient = horizontal_gradient(S, [(0.00, BRASS_DARK), (0.16, BRASS_MID), (0.38, BRASS_LIGHT),
                                           (0.62, BRASS_MID), (0.86, BRASS_DARK), (1.00, BRASS_DARK)])
    else:
        gradient = horizontal_gradient(S, [(0.00, BRASS_DARK), (0.30, BRASS_MID), (0.50, BRASS_LIGHT),
                                           (0.75, BRASS_MID), (1.00, BRASS_DARK)])

    metal = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    metal.paste(gradient, (0, 0), casing)
    img.alpha_composite(metal)

    detail = ImageDraw.Draw(img, "RGBA")

    # shadow seam where the case head meets the rim
    detail.rectangle((px(base_x0), px(base_y - 0.010), px(base_x1), px(base_y)),
                     fill=(96, 66, 32, 120))

    groove_y0, groove_y1 = (0.762, 0.780) if detailed else (0.762, 0.782)
    detail.rounded_rectangle((px(rim_x0 + 0.010), px(groove_y0), px(rim_x1 - 0.010), px(groove_y1)),
                             radius=int(S * 0.006), fill=BRASS_DARK + (200,))

    if detailed:
        r = 0.028
        cy = 0.802
        detail.ellipse((px(0.5 - r), px(cy - r), px(0.5 + r), px(cy + r)), fill=MOUTH + (235,))
        detail.ellipse((px(0.5 - r * 0.55), px(cy - r * 0.55), px(0.5 + r * 0.55), px(cy + r * 0.55)),
                       fill=(166, 126, 72, 200))

        # case mouth: dark opening with a lit rim so it reads as a hollow casing
        detail.ellipse((px(neck_x0 + 0.010), px(neck_y0 - 0.018), px(neck_x1 - 0.010), px(neck_y0 + 0.024)),
                       fill=(150, 112, 62, 235))
        detail.ellipse((px(neck_x0 + 0.016), px(neck_y0 - 0.012), px(neck_x1 - 0.016), px(neck_y0 + 0.018)),
                       fill=MOUTH + (250,))
        detail.ellipse((px(neck_x0 + 0.040), px(neck_y0 - 0.002), px(neck_x1 - 0.040), px(neck_y0 + 0.010)),
                       fill=(18, 12, 8, 255))

        spec = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        ImageDraw.Draw(spec).rectangle((px(0.414), px(neck_y0 + 0.032), px(0.432), px(base_y - 0.024)),
                                       fill=(255, 246, 226, 110))
        spec = spec.filter(ImageFilter.GaussianBlur(int(S * 0.004)))
        img.alpha_composite(Image.composite(spec, Image.new("RGBA", (S, S), (0, 0, 0, 0)), casing))
    else:
        detail.rectangle((px(neck_x0), px(neck_y0), px(neck_x1), px(neck_y0 + 0.030)), fill=MOUTH + (245,))

    return img


detailed = build()
simplified = build(detailed=False)

detailed.save(OUT + "master-1024.png")
detailed.resize((256, 256), Image.LANCZOS).save(OUT + "preview-256.png")
simplified.resize((32, 32), Image.LANCZOS).save(OUT + "preview-32.png")
simplified.resize((16, 16), Image.LANCZOS).save(OUT + "preview-16.png")

plan = [(16, simplified), (24, simplified), (32, simplified),
        (48, detailed), (64, detailed), (128, detailed), (256, detailed)]
frames = [src.resize((size, size), Image.LANCZOS) for size, src in plan]
frames[-1].save(OUT + "spt-quickstart.ico", format="ICO",
                sizes=[(size, size) for size, _ in plan], append_images=frames[:-1])
print("ok")
