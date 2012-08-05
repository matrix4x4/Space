﻿def goto(x, y):
	component = manager.GetComponent(avatar, Transform.TypeId)
	component.SetTranslation(x, y)
	component.ApplyTranslation()

def setBaseStat(type, value):
    character.SetBaseValue(type, value)

def desync():
	import sys
	sys.path.append("C:\\Program Files (x86)\\IronPython 2.7.1\\Lib")
	import random
	translation = manager.GetComponent(avatar, Transform.TypeId).Translation
	goto(translation.X + random.random(), translation.Y + random.random())