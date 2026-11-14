-- 14 source columns at x=3; head y=0..12, clothes y=13..19.
-- Every proposal is left-front three-quarter view, not a different animation frame.
local head={
 '.....OOOOOO...',
 '...OOWWWWWWOO.',
 '..OWWWWWWWLLO.',
 '.OWWWWWWLLLLO.',
 '.OWWLLPPPLLSO.',
 '.OWLLPPPPLLSO.',
 '.OLPPPPPQLLSO.',
 'OPPPPPPPQLLSO.',
 'OPOPPOPPQLLSO.',
 'OPPPPOPPQLLSO.',
 '.OPPPPPPQLLSO.',
 '..OPQQPPLLSO..',
 '...OOOOOLSO...',
}
local body={
 '...OWKLWO.....',
 '..OWWLKWO.....',
 '...OKKKKO.....',
 '...OKDDKO.....',
 '...OKKKKO.....',
 '..OKKOOKO.....',
 '..OOO.........',
}
local cape={
 '...OW..WWO....',
 '..OWW..WLWO...',
 '.OWLO..WLLWO..',
 '.OWO...WLWLWO.',
 '.OO....OLWWWO.',
 '........OOOO..',
 '..............',
}
local function copy(t)
 local c={};for i,v in ipairs(t) do c[i]=v end;return c
end
local function concept(id,slug,title,note,h,b,c)
 local result={id=id,slug=slug,title=title,note=note,head=copy(head),body=copy(body),cape=copy(cape)}
 for k,v in pairs(h or {}) do result.head[k]=v end
 for k,v in pairs(b or {}) do result.body[k]=v end
 for k,v in pairs(c or {}) do result.cape[k]=v end
 return result
end
return {
 concept('01','swept-bob','SWEPT BOB','BASE / SPLIT COAT'),
 concept('02','round-bob','ROUND BOB','ROUND HAIR / SOFT HEM',{
  [1]='....OOOOOO....',
  [2]='..OOWWWWWWOO..',
  [3]='.OWWWWWWWWWLO.',
  [4]='OWWWWWWWWLLLSO',
  [5]='OWWWLLPPPLLLSO',
  [6]='OWWLLPPPPLLLSO',
  [7]='OWLPPPPPQLLLSO',
  [11]='.OPPPPPPQLLSSO',
  [12]='..OPQQPPLSSSO.',
  [13]='...OOOOOSOOO..',
 },{}, {
  [4]='.OWO...WLLWO..',
  [5]='.OO....OLWWO..',
  [6]='........OOO...',
 }),
 concept('03','short-bob','SHORT BOB','CROPPED HAIR / SHORT CAPE',{
  [1]='....OOOOOO....',
  [2]='..OOWWWWWWOO..',
  [3]='.OWWWWWWWLLLO.',
  [4]='.OWWWWLLLLSSO.',
  [5]='.OWWLPPPPLSSO.',
  [6]='.OWLPPPPPQLSO.',
  [10]='OPPPPOPPQQOO..',
  [11]='.OPPPPPPQQO...',
  [12]='..OPQQPPQO....',
  [13]='...OOOOOO.....',
 },{}, {
  [3]='.OWLO..WLLWO..',
  [4]='.OWO...OLLWO..',
  [5]='.OO.....OOO...',
  [6]='..............',
 }),
 concept('04','long-bob','LONG BOB','LONG SIDES / LONG COAT',{
  [3]='..OWWWWWWWWLO.',
  [4]='.OWWWWWWWLLLO.',
  [5]='.OWWWLPPPLLSO.',
  [6]='.OWWLPPPPLLSO.',
  [11]='.OPPPPPPQLLLSO',
  [12]='..OPQQPPLLLLSO',
  [13]='...OOOOOLLLSO.',
 },{}, {
  [4]='.OWO...WLWWLO.',
  [5]='.OWO...OLWWWO.',
  [6]='..O.....OLLO..',
 }),
 concept('05','high-knot','HIGH KNOT','HIGH TIE / LIGHT COAT',{
  [1]='........OOOO..',
  [2]='.......OWWLO..',
  [3]='....OOOWWLO...',
  [4]='..OOWWWWLOOO..',
  [5]='.OWWWLPPPLLSO.',
  [6]='.OWWLPPPPLLSO.',
  [12]='..OPQQPPLSO...',
  [13]='...OOOOOOO....',
 },{
  [1]='...OWWLWO.....',
  [2]='..OWLWKWO.....',
 }),
 concept('06','low-knot','LOW KNOT','LOW TIE / REAR DRAPE',{
  [4]='.OWWWWWWLLLSOO',
  [5]='.OWWLLPPPLSOWO',
  [6]='.OWLLPPPPLSOWO',
  [7]='.OLPPPPPQLSOWO',
  [8]='OPPPPPPPQLLSOO',
  [12]='..OPQQPPLLSO..',
 },{}, {
  [4]='.OWO...WLLWWLO',
  [5]='.OO....OLWWWLO',
  [6]='........OOOOO.',
 }),
 concept('07','pinned-bob','PINNED BOB','DARK PIN / DARK EDGES',{
  [3]='..OWWWWWWWLLO.',
  [4]='.OWWWWLLLLLLO.',
  [5]='.OWWLLPPPLKKO.',
  [6]='.OWLPPPPPLLSO.',
 },{
  [1]='...OWKKWO.....',
  [2]='..OWLKKWO.....',
 },{
  [3]='.OWKO..WKLWO..',
  [4]='.OWO...WLWKWO.',
  [5]='.OO....OKWWKO.',
 }),
 concept('08','ink-tips','INK TIPS','WHITE HAIR / DARK TIPS',{
  [2]='...OOWWWWWOO..',
  [3]='..OWWWWWWWLLO.',
  [4]='.OWWWWWWLLLLSO',
  [5]='.OWWWLLPPLLLSO',
  [6]='.OWWLLPPPLLLSO',
  [7]='.OWLPPPPQLLLSO',
  [10]='OPPPPOPPQDDKO.',
  [11]='.OPPPPPPQDDKO.',
  [12]='..OPQQPPDDKO..',
  [13]='...OOOOODKO...',
 },{}, {
  [4]='.OWO...WLWWWO.',
  [5]='.OO....OLLLLO.',
 }),
 concept('09','high-collar','HIGH COLLAR','OPEN FACE / HIGH COLLAR',{
  [1]='.....OOOOO....',
  [2]='...OOWWWWWOO..',
  [3]='..OWWWWWWWLLO.',
  [4]='.OWWWWLLLLSSO.',
  [5]='.OWWLPPPPLSSO.',
  [6]='.OWLPPPPPQLSO.',
  [7]='.OLPPPPPQLLSO.',
 },{
  [1]='...OKKKWO.....',
  [2]='..OWKKKWO.....',
  [3]='...OLKKKO.....',
 },{
  [3]='.OWLO..WLWWO..',
  [4]='.OWO...WLWWLO.',
 }),
 concept('10','flared-cape','FLARED CAPE','SWEPT HAIR / WIDE HEM',{
  [1]='......OOOOO...',
  [2]='....OOWWWWWOO.',
  [3]='..OOWWWWWWLLO.',
  [4]='.OWWWWWWLLLLSO',
  [5]='.OWWWLPPPLLLSO',
  [6]='.OWWLPPPPLLLSO',
  [11]='.OPPPPPPQLLLSO',
  [12]='..OPQQPPLLLSO.',
  [13]='...OOOOOLLSO..',
 },{
  [1]='...OWWLWO.....',
  [2]='..OWWLKWO.....',
 },{
  [3]='.OWLO..WLWWWO.',
  [4]='OWWO...WLLWWLO',
  [5]='OWLO...OLWWWLO',
  [6]='OOO.....OOOOO.',
 }),
}
