-- Export the saved editable file without writing back to the native source.
local input = app.params["input"]
local output = app.params["output"]
local variant = app.params["variant"] or "attack1"
assert(input and output, "Missing input or output path")
local sprite = app.open(input)
assert(sprite, "Cannot open editable VFX")
assert(sprite.width == 70 and sprite.height == 66, "Keep the shared 70x66 canvas")
assert(#sprite.frames == 4, "Expected four VFX images")
app.fs.makeAllDirectories(output)

for _, layer in ipairs(sprite.layers) do
  if layer.name == "REFERENCE_ORIGINAL" or layer.name == "GUIDE_PIVOT" then
    layer.isVisible = false
  end
end

local sheet = Image(280, 66, ColorMode.RGB)
local frames = {}
for i = 1, 4 do
  local image = Image(sprite.spec)
  image:drawSprite(sprite, i, Point(0, 0))
  local name = string.format("slash-%02d.png", i)
  image:saveAs(output .. "/" .. name)
  sheet:drawImage(image, Point((i - 1) * 70, 0))
  frames[i] = {file=name, order=i, rect={x=(i-1)*70,y=0,w=70,h=66}}
end
sheet:saveAs(output .. "/slash-sheet.png")
local data = {
  canvas={w=70,h=66},
  pivot={x=35,y=33,origin="top-left"},
  pixelsPerUnit=16,
  frames=frames,
  unityTiming="User will specify the attack frame for each image; native preview timing is not authoritative",
  variant=variant,
  horizontalFlip=(variant == "attack2"),
  directionColors={attack1Original="#E5D2FF",attack2Mirrored="#C3EAFF"},
  damagePolicy=(variant == "attack3") and "Two visual slashes, one stronger damage event; no per-slash hit events" or nil,
  combinedSlashes=(variant == "attack3") and {source="attack1 plus mirrored attack2 without shape changes",centerX=35,leftFront="blue",rightFront="purple",startAttackFrame=31,lastVisibleAttackFrame=42,bothAbsentFromFrame=43} or nil,
  secondAttack="Use the pale-blue attack2 file with horizontal flip; attack1 is pale lavender"
}
local file = assert(io.open(output .. "/frames.json", "w"))
file:write(json.encode(data))
file:close()
print("Exported four images and a sheet: " .. output)
