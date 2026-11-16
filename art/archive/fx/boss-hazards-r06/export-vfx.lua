-- 편집 원본의 현재 레이어를 몸체와 광륜으로 분리 출력
local input=app.params["input"]
local output=app.params["output"]
local name=app.params["name"]
assert(input and output and name,"input, output and name are required")
local sprite=app.open(input)
assert(sprite and sprite.width==32 and sprite.height==32,"A 32x32 VFX source is required")
local visible={}
for i,layer in ipairs(sprite.layers) do visible[i]=layer.isVisible end
local function saveImage(image,path)
  local target=Sprite(image.width,image.height,ColorMode.RGB)
  target:newCel(target.layers[1],1,Image(image),Point(0,0))
  target:saveCopyAs(path)
end
local function render()
  local image=Image(sprite.spec)
  image:drawSprite(sprite,1)
  return image
end
sprite:saveCopyAs(output.."/"..name.."-composite.png")
for _,layer in ipairs(sprite.layers) do
  if layer.name=="Halo" then layer.isVisible=false end
end
local body=Image(18,18,ColorMode.RGB)
body:clear()
body:drawImage(render(),Point(-7,-7))
saveImage(body,output.."/"..name.."-bullet.png")
for _,kind in ipairs({"Halo"}) do
  for _,layer in ipairs(sprite.layers) do layer.isVisible=layer.name==kind end
  saveImage(render(),output.."/"..name.."-halo.png")
end
for i,layer in ipairs(sprite.layers) do layer.isVisible=visible[i] end
