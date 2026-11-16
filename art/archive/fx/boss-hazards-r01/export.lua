-- 사용자 편집 원본을 다시 그리지 않고 현재 합성만 출력
local input = app.params["input"]
local output = app.params["output"]
assert(input and output, "input and output are required")
local sprite = app.open(input)
assert(sprite, "Cannot open bullet source")
sprite:saveCopyAs(output)
