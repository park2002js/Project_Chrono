-- R05 원화의 픽셀을 바꾸지 않고 몸통과 두 손을 실행용 이미지로 분리
local input=app.params["input"]
local output=app.params["output"]
assert(input and output,"input and output are required")
local sprite=app.open(input)
assert(sprite and sprite.width==192 and sprite.height==120,"R05 guard source must be192x120")
local visible={}
for i,layer in ipairs(sprite.layers) do visible[i]=layer.isVisible end
local hands={"06_Fist_Left_BackFacesLeft","07_Fist_Right_BackFacesRight"}
local function render()
  local image=Image(sprite.spec)
  image:drawSprite(sprite,1)
  return image
end
local function saveImage(image,path)
  local target=Sprite(image.width,image.height,ColorMode.RGB)
  target:newCel(target.layers[1],1,Image(image),Point(0,0))
  target:saveCopyAs(path)
end
for _,layer in ipairs(sprite.layers) do
  if layer.name==hands[1] or layer.name==hands[2] then layer.isVisible=false end
end
saveImage(render(),output.."/guard-body.png")
for i,name in ipairs(hands) do
  local found=false
  for _,layer in ipairs(sprite.layers) do
    layer.isVisible=layer.name==name
    if layer.isVisible then found=true end
  end
  assert(found,"Expected hand layer is missing: "..name)
  local image=Image(34,40,ColorMode.RGB)
  image:clear()
  image:drawImage(render(),Point(i==1 and -7 or -151,-64))
  saveImage(image,output..(i==1 and "/guard-hand-left.png" or "/guard-hand-right.png"))
end
for i,layer in ipairs(sprite.layers) do layer.isVisible=visible[i] end
sprite:saveCopyAs(output.."/guard-separated.aseprite")
