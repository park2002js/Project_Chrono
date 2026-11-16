-- 16px 도트 영역의 네 변 중앙에 각각 8px 평면을 두어 마름모화를 방지
local output=app.params["output"]
assert(output,"output is required")
local function filled(x,y)
  if x<0 or x>15 or y<0 or y>15 then return false end
  local distance=math.min(y,15-y)
  local width=distance<4 and 8+2*distance or 16
  local left=(16-width)/2
  return x>=left and x<left+width
end
local palettes={
  normal={edge=Color{r=122,g=23,b=45,a=255},body=Color{r=244,g=66,b=80,a=255},core=Color{r=255,g=247,b=239,a=255}},
  gold={edge=Color{r=168,g=108,b=21,a=255},body=Color{r=255,g=196,b=61,a=255},core=Color{r=255,g=250,b=224,a=255}}
}
for _,name in ipairs({"normal","gold"}) do
  local palette=palettes[name]
  local sprite=Sprite(18,18,ColorMode.RGB)
  local images={edge=Image(18,18,ColorMode.RGB),body=Image(18,18,ColorMode.RGB),core=Image(18,18,ColorMode.RGB)}
  for _,image in pairs(images) do image:clear() end
  for y=0,15 do
    for x=0,15 do
      if filled(x,y) then
        local boundary=false
        for dy=-1,1 do
          for dx=-1,1 do
            if not filled(x+dx,y+dy) then boundary=true end
          end
        end
        local key=boundary and "edge" or "body"
        if (x==7 or x==8) and (y==7 or y==8) then key="core" end
        images[key]:drawPixel(x+1,y+1,palette[key])
      end
    end
  end
  for index,key in ipairs({"edge","body","core"}) do
    local layer=index==1 and sprite.layers[1] or sprite:newLayer()
    layer.name=key=="edge" and "Flat8pxOutline" or key=="body" and "Body" or "Core"
    sprite:newCel(layer,1,images[key],Point(0,0))
  end
  sprite:saveAs(output.."/"..name.."-bullet.aseprite")
  sprite:saveCopyAs(output.."/"..name.."-bullet.png")
end
