# 用 svglib 渲染接触表：每行拼一个 SVG，最后拼成一张网格图
import glob, os
from svglib.svglib import svg2rlg
from reportlab.graphics import renderPM
from PIL import Image

files = sorted(glob.glob("nav_*.svg"))
# 用名称排序分组：每张 tile 128
tile = 112
gap = 14
cols = 7
rows = (len(files)+cols-1)//cols
sheet = Image.new("RGB", (cols*(tile+gap)+gap, rows*(tile+66)+gap), (243,246,251))
from PIL import ImageDraw
d = ImageDraw.Draw(sheet)
for i, f in enumerate(files):
    try:
        dwg = svg2rlg(f)
        if dwg is None: raise ValueError("parse fail")
        dwg.width = tile; dwg.height = tile
        dwg.scale(tile/128, tile/128)
        png = os.path.join(os.environ.get("TEMP","."), f"_tile{i}.png")
        renderPM.drawToFile(dwg, png, fmt="PNG", dpi=192, bg=0xF3F6FB)
        im = Image.open(png).convert("RGB").resize((tile,tile))
    except Exception as e:
        im = Image.new("RGB",(tile,tile),(255,220,220))
    r,c = divmod(i, cols)
    x = gap + c*(tile+gap); y = gap + r*(tile+66)
    sheet.paste(im, (x,y))
    name = os.path.basename(f)[4:-4]
    d.text((x, y+tile+6), name, fill=(27,37,64))
sheet.save("_sheet.png")
print("rows",rows,"tiles",len(files), sheet.size)
