"""Validate backward-compatible story rows and strict optional timing metadata."""
import importlib.util
import math
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parent.parent
spec=importlib.util.spec_from_file_location('campaign_compiler',ROOT/'Tools/compile-campaign.py')
compiler=importlib.util.module_from_spec(spec)
spec.loader.exec_module(compiler)

class StoryRowTests(unittest.TestCase):
    def test_legacy_two_and_three_items(self):
        self.assertEqual(('카이','대사','',None),compiler.story_line(['카이','대사']))
        self.assertEqual(('','서술','music_cue',None),compiler.story_line(['','서술','music_cue']))

    def test_metadata_with_or_without_music(self):
        self.assertEqual(('리안','대사','',4.8),compiler.story_line(['리안','대사','',{'autoDelay':4.8}]))
        self.assertEqual(('리안','대사','cue',1),compiler.story_line(['리안','대사','cue',{'autoDelay':1}]))
        self.assertEqual(6,compiler.story_line(['','서술','',{'autoDelay':6}])[3])

    def test_invalid_structure_rejected(self):
        for row in (None,{},(),[],['카이'],['카이','대사','','',0],
                    [None,'대사'],['카이',None],['카이','  '],
                    ['카이','대사',{'autoDelay':4}],['카이','대사',None],
                    ['카이','대사','',4],['카이','대사','',{}],
                    ['카이','대사','',{'autoDelay':4,'other':1}]):
            with self.subTest(row=row),self.assertRaises(AssertionError):
                compiler.story_line(row)

    def test_invalid_delay_rejected(self):
        for value in (True,False,None,'4.8',0,0.99,6.01,-1,math.nan,math.inf,-math.inf):
            with self.subTest(value=value),self.assertRaises(AssertionError):
                compiler.story_line(['','서술','',{'autoDelay':value}])

if __name__=='__main__':unittest.main()
