-- Native Aseprite authoring: one reusable frame with four independently editable layers.
local output = assert(app.params["output"], "Missing output")
app.fs.makeAllDirectories(output)
local W, H, B = 256, 64, 12
local rgba = app.pixelColor.rgba
local palette = {
  clear=0, shadow=rgba(2,4,10,255), rim=rgba(7,12,22,255),
  navy=rgba(14,24,43,255), blue=rgba(28,47,76,255),
  lightBlue=rgba(37,62,96,255), innerBlue=rgba(16,29,50,255),
  panel=rgba(7,10,18,255), violet=rgba(48,29,68,255),
  violetLight=rgba(68,43,88,255), violetShadow=rgba(30,20,46,255)
}
local sprite = Sprite(W,H,ColorMode.RGB)
sprite.filename = output .. "/window-frame.aseprite"
sprite.layers[1].name = "01_Outer_Shadow"

local function rect(image,x,y,w,h,c)
  for yy=y,y+h-1 do for xx=x,x+w-1 do image:drawPixel(xx,yy,c) end end
end
local function chamfer(image,x,y,w,h,r,c)
  for yy=0,h-1 do
    local cut=math.max(0,r-math.min(yy,h-1-yy))
    rect(image,x+cut,y+yy,w-cut*2,1,c)
  end
end
local function layer(name,draw)
  local target = name == "01_Outer_Shadow" and sprite.layers[1] or sprite:newLayer()
  target.name=name
  local image=Image(W,H,ColorMode.RGB)
  draw(image)
  sprite:newCel(target,1,image,Point(0,0))
end

layer("01_Outer_Shadow",function(image)
  chamfer(image,0,0,W,H,7,palette.shadow)
end)
layer("02_Blue_Bevel",function(image)
  chamfer(image,1,1,254,62,6,palette.rim)
  chamfer(image,2,2,252,60,5,palette.navy)
  chamfer(image,3,3,250,58,4,palette.blue)
  -- Uniform central edge strips keep nine-slice seams invisible.
  rect(image,8,2,240,1,palette.blue)
  rect(image,8,3,240,1,palette.lightBlue)
  rect(image,3,8,1,48,palette.lightBlue)
  rect(image,4,8,1,48,palette.blue)
  rect(image,251,8,1,48,palette.innerBlue)
  rect(image,8,60,240,1,palette.innerBlue)
  chamfer(image,5,5,246,54,2,palette.innerBlue)
end)
layer("03_Near_Black_Fill",function(image)
  chamfer(image,6,6,244,52,1,palette.panel)
end)
layer("04_Dark_Violet_Inlays",function(image)
  -- Every inlay is entirely inside a fixed 12x12 corner.
  local function mark(x,y,c)
    image:drawPixel(x,y,c)
    image:drawPixel(W-1-x,y,c)
    image:drawPixel(x,H-1-y,c)
    image:drawPixel(W-1-x,H-1-y,c)
  end
  for x=7,10 do mark(x,3,palette.violetLight); mark(x,4,palette.violet) end
  for x=5,7 do mark(x,5,palette.violetLight); mark(x,6,palette.violet) end
  for y=7,10 do mark(3,y,palette.violet); mark(4,y,palette.violetShadow) end
  mark(5,7,palette.violet)
end)

local colors={palette.clear,palette.shadow,palette.rim,palette.navy,palette.blue,
  palette.lightBlue,palette.innerBlue,palette.panel,palette.violet,
  palette.violetLight,palette.violetShadow}
local swatches=Palette(#colors)
for i,value in ipairs(colors) do swatches:setColor(i-1,Color(value)) end
sprite:setPalette(swatches)
local slice=sprite:newSlice(Rectangle(0,0,W,H))
slice.name="window-frame"
slice.center=Rectangle(B,B,W-B*2,H-B*2)
slice.pivot=Point(W//2,H//2)
slice.data="9-slice L/R/T/B=12px; separate UI text; opaque center; integer scale + Point"
sprite:saveAs(sprite.filename)
local flat=Image(sprite)
flat:saveAs(output.."/window-frame.png")
print("Created 256x64 frame, 4 editable layers, 12px nine-slice")
