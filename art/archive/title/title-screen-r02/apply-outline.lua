-- Preserve the original ten layers and add an editable one-pixel silhouette.
local root = assert(app.params["output"], "Missing output directory")
local sprite = assert(app.open(root .. "/before-title-logo-17-natural-cut.aseprite"), "Cannot open original")
app.activeSprite = sprite
local outline = Image{ fromFile = root .. "/grip-saya-outline-1px.png" }
local layer = sprite:newLayer()
layer.name = "11 White grip and saya outline 1px"
layer.stackIndex = 10 -- Keep the original cross highlight above the new outline.
sprite:newCel(layer, sprite.frames[1], outline, Point(0, 0))
sprite:saveAs(root .. "/title-logo-17-natural-cut.aseprite")
sprite:saveCopyAs(root .. "/title-logo-17-natural-cut.png")
print("Saved original layers plus editable white outline")
