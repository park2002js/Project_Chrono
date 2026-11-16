-- 8px 편집 원본과 Unity용 PNG를 같은 픽셀에서 작성
local output = app.params["output"]
assert(output and #output > 0, "output is required")
local sprite = Sprite(8, 8, ColorMode.RGB)
sprite.layers[1].name = "Outline"
local rows = {
  "........",
  "..dddd..",
  ".drrrrd.",
  ".drwwrd.",
  ".drwwrd.",
  ".drrrrd.",
  "..dddd..",
  "........"
}
local colors = {
  d = Color{ r=122, g=23, b=45, a=255 },
  r = Color{ r=244, g=66, b=80, a=255 },
  w = Color{ r=255, g=247, b=239, a=255 }
}
local names = { d="Outline", r="RedBody", w="WhiteCore" }
for index, key in ipairs({"d", "r", "w"}) do
  local layer = index == 1 and sprite.layers[1] or sprite:newLayer()
  layer.name = names[key]
  local image = Image(8, 8, ColorMode.RGB)
  image:clear()
  for y=0,7 do
    for x=0,7 do
      if rows[y+1]:sub(x+1,x+1) == key then image:drawPixel(x,y,colors[key]) end
    end
  end
  sprite:newCel(layer, 1, image, Point(0,0))
end
sprite:saveAs(output .. "/normal-bullet.aseprite")
sprite:saveCopyAs(output .. "/normal-bullet.png")
