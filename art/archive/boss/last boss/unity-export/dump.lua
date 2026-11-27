-- Aseprite 원본 한 개를 레이어·프레임별 전체 캔버스 PNG와 정보 파일로 풀어 놓는다
local path = app.params["f"]
local out = app.params["o"]
local sprite = app.open(path)
if not sprite then error("cannot open " .. path) end

local function esc(s) return (s:gsub('\\', '\\\\'):gsub('"', '\\"')) end

local layers = {}
local function collect(list)
  for _, layer in ipairs(list) do
    if layer.isGroup then collect(layer.layers) else layers[#layers + 1] = layer end
  end
end
collect(sprite.layers)

local parts = {}
parts[#parts + 1] = string.format('{"width":%d,"height":%d,"layers":[', sprite.width, sprite.height)
for i, layer in ipairs(layers) do
  parts[#parts + 1] = string.format('%s"%s"', i > 1 and "," or "", esc(layer.name))
end
parts[#parts + 1] = '],"durations":['
for i, frame in ipairs(sprite.frames) do
  parts[#parts + 1] = string.format('%s%d', i > 1 and "," or "", math.floor(frame.duration * 1000 + 0.5))
end
parts[#parts + 1] = '],"tags":['
for i, tag in ipairs(sprite.tags) do
  parts[#parts + 1] = string.format('%s{"name":"%s","from":%d,"to":%d}', i > 1 and "," or "", esc(tag.name),
    tag.fromFrame.frameNumber, tag.toFrame.frameNumber)
end
parts[#parts + 1] = '],"cels":['
local first = true
for li, layer in ipairs(layers) do
  for _, cel in ipairs(layer.cels) do
    if not cel.image:isEmpty() then
      local canvas = Image(sprite.width, sprite.height, sprite.colorMode)
      canvas:drawImage(cel.image, cel.position)
      local name = string.format("L%02d_F%03d.png", li, cel.frameNumber)
      canvas:saveAs(out .. "/" .. name)
      parts[#parts + 1] = string.format('%s[%d,%d]', first and "" or ",", li, cel.frameNumber)
      first = false
    end
  end
end
parts[#parts + 1] = ']}'
local file = io.open(out .. "/info.json", "w")
file:write(table.concat(parts))
file:close()
