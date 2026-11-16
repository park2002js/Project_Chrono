-- 같은 몸체와 광륜의 빨강을 금색으로 치환하고 추가 링 없이 구분
local output=app.params["output"]
assert(output,"output is required")
local function filled(x,y)
  if x<0 or x>15 or y<0 or y>15 then return false end
  local d=math.min(y,15-y)
  local width=d<4 and 8+2*d or 16
  return x>=(16-width)/2 and x<(16+width)/2
end
local function distanceToBody(x,y)
  local best=10000
  for by=0,15 do
    for bx=0,15 do
      if filled(bx,by) then best=math.min(best,(x-bx)*(x-bx)+(y-by)*(y-by)) end
    end
  end
  return math.sqrt(best)
end
local function coreFilled(x,y)
  x,y=x-2,y-2
  if x<0 or x>11 or y<0 or y>11 then return false end
  local d=math.min(y,11-y)
  local width=d<3 and 6+2*d or 12
  return x>=(12-width)/2 and x<(12+width)/2
end
local function saveImage(image,path)
  local sprite=Sprite(image.width,image.height,ColorMode.RGB)
  sprite:newCel(sprite.layers[1],1,Image(image),Point(0,0))
  sprite:saveCopyAs(path)
end
for _,name in ipairs({"normal","gold"}) do
  local sprite=Sprite(32,32,ColorMode.RGB)
  local halo=Image(32,32,ColorMode.RGB)
  local rim=Image(32,32,ColorMode.RGB)
  local core=Image(32,32,ColorMode.RGB)
  for _,image in ipairs({halo,rim,core}) do image:clear() end
  for y=0,31 do
    for x=0,31 do
      local bx,by=x-8,y-8
      if filled(bx,by) then
        rim:drawPixel(x,y,name=="gold" and Color{r=255,g=208,b=52,a=255} or Color{r=255,g=38,b=48,a=255})
        if coreFilled(bx,by) then core:drawPixel(x,y,Color{r=239,g=248,b=252,a=255}) end
      else
        local distance=distanceToBody(bx,by)
        if distance<=6 then
          local alpha=math.floor(160*math.exp(-distance*distance/(2*1.9*1.9))+.5)
          local color=name=="gold" and Color{r=255,g=181,b=32,a=alpha} or Color{r=255,g=27,b=36,a=alpha}
          halo:drawPixel(x,y,color)
        end
      end
    end
  end
  local images={halo,rim,core}
  local names={"Halo","DangerRim","BrightCore"}
  for index,image in ipairs(images) do
    local layer=index==1 and sprite.layers[1] or sprite:newLayer()
    layer.name=names[index]
    sprite:newCel(layer,1,image,Point(0,0))
  end
  sprite:saveAs(output.."/"..name.."-bullet.aseprite")
  sprite:saveCopyAs(output.."/"..name.."-composite.png")

  -- 몸체는 기존18px셀에 자르고 광륜은32px공통중심을 유지
  local body=Image(18,18,ColorMode.RGB)
  body:clear()
  body:drawImage(rim,Point(-7,-7))
  body:drawImage(core,Point(-7,-7))
  saveImage(body,output.."/"..name.."-bullet.png")
  saveImage(halo,output.."/"..name.."-halo.png")
end
