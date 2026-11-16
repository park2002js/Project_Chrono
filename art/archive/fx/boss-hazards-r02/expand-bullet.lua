-- 중심 도트를 보존하고 빨간 몸체와 바깥 윤곽을 각 방향 2px 확장
local input = app.params["input"]
local output = app.params["output"]
assert(input and output, "input and output are required")
local source = app.open(input)
assert(source and source.width == 8 and source.height == 8, "An 8x8 source is required")
local original = Image(source.spec)
original:drawSprite(source, 1)
local sprite = Sprite(12, 12, ColorMode.RGB)
sprite.layers[1].name = "ExpandedRim2px"
local outline = Image(12, 12, ColorMode.RGB)
outline:clear()
local edge = Color{r=122, g=23, b=45, a=255}
local body = Color{r=244, g=66, b=80, a=255}
for y=0,11 do
  for x=0,11 do
    local sx, sy = x-2, y-2
    local inside = sx>=0 and sx<8 and sy>=0 and sy<8 and app.pixelColor.rgbaA(original:getPixel(sx,sy))>0
    if not inside then
      local near, inner = false, false
      for dy=-2,2 do
        for dx=-2,2 do
          local px, py = sx+dx, sy+dy
          if dx*dx+dy*dy<=4 and px>=0 and px<8 and py>=0 and py<8 and app.pixelColor.rgbaA(original:getPixel(px,py))>0 then
            near=true
            if dx*dx+dy*dy<=1 then inner=true end
          end
        end
      end
      if near then outline:drawPixel(x,y,inner and body or edge) end
    end
  end
end
sprite:newCel(sprite.layers[1],1,outline,Point(0,0))
for _, layer in ipairs(source.layers) do
  local cel = layer:cel(1)
  if cel then
    local target = sprite:newLayer()
    target.name = layer.name == "Outline" and "ExpandedBody" or layer.name
    target.opacity = layer.opacity
    local image = Image(cel.image)
    if layer.name == "Outline" then
      for y=0,image.height-1 do
        for x=0,image.width-1 do
          if app.pixelColor.rgbaA(image:getPixel(x,y))>0 then image:drawPixel(x,y,body) end
        end
      end
    end
    sprite:newCel(target,1,image,Point(cel.position.x+2,cel.position.y+2))
  end
end
sprite:saveAs(output .. "/normal-bullet.aseprite")
sprite:saveCopyAs(output .. "/normal-bullet.png")
