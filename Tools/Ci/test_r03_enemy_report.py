import importlib.util
from pathlib import Path
import unittest

spec=importlib.util.spec_from_file_location("r03",Path(__file__).parents[1]/"Diagnostics/r03_enemy_report.py")
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)


def sample(t,x=0,sprite="jump",visible=1,paused=0,scene="Stage_3-1"):
    return {"kind":"r03_enemy_sample","scene":scene,"detail":f"id=1;ptime={t};sprite={sprite};visible={visible};paused={paused}","position":{"x":x,"y":0}}


class R03ReportTests(unittest.TestCase):
    def test_death_cleanup_reports_late_shadow_and_missing_death_without_inventing_acceptance(self):
        definitions=[{"kind":"r03_enemy_definition","scene":"Stage_3-1","detail":f"id={i};owner=4;family=Frog;branch={branch}"}
                     for i,branch in [(1,"Shadow"),(2,"Death")]]
        death=sample(1.05);death["detail"]=death["detail"].replace("id=1;","id=2;")
        result=r.death_cleanup(definitions+[sample(1),death],{"observer":[sample(1.5)]})["observer"][0]
        self.assertEqual(result["deathVisibleSamples"],0)
        self.assertAlmostEqual(result["maxShadowAfterDeathMs"],450)
        self.assertAlmostEqual(result["shadowBeyondAuthorityMs"],500)
        native=r.death_cleanup(definitions+[death,sample(1.5)],{"observer":[sample(1.48)]})["observer"][0]
        self.assertEqual(native["shadowBeyondAuthorityMs"],0,"Native death shadows are preserved, not hidden early.")
        self.assertEqual(r.death_cleanup([], {"observer":[sample(1)]})["observer"],[])

    def test_truncated_dropped_or_empty_capture_is_not_valid(self):
        self.assertTrue(r.capture_health([sample(1)])["valid"])
        for rows in ([],[{"kind":"capture_limit"}],[dict(sample(1),dropped=4)]):
            self.assertFalse(r.capture_health(rows)["valid"])

    def test_native_reference_preserves_collision_deviation_and_phase_coverage(self):
        rows=[{"kind":"r03_native_curve","detail":f"family=Hop;progress={p};groundErrorPx={g};liftErrorPx=0.1"}
              for p,g in [(0,0),(.5,8),(1,0)]]
        result=r.native_curves(rows)["Hop"]
        self.assertEqual(result["samples"],3);self.assertEqual(result["groundMaxPx"],8)
        self.assertEqual(result["progressMin"],0);self.assertEqual(result["progressMax"],1)

    def test_missing_native_reference_is_not_a_zero_error_baseline(self):
        self.assertEqual(r.native_curves([sample(1)]),{})

    def test_waterline_and_material_must_match_approved_state(self):
        source=[sample(1),sample(1.05)]
        for row in source: row["detail"]+=";water=1;surface=2;line=0.15;useLine=1;premultiply=0"
        client=sample(1.025)
        client["detail"]+=";water=1;surface=2;line=0.15;useLine=1;premultiply=0"
        result=self.compare([client],source)["Frog"]
        self.assertEqual(result["waterChecked"],1);self.assertEqual(result["waterMismatch"],0)
        self.assertEqual(result["waterActiveChecked"],1)
        client["detail"]=client["detail"].replace("surface=2","surface=3")
        self.assertEqual(self.compare([client],source)["Frog"]["waterMismatch"],1)
        client["detail"]=client["detail"].replace("water=1","water=0")
        self.assertEqual(self.compare([client],source)["Frog"]["waterMismatch"],1)

    def compare(self,client,server=None):
        server=server or [sample(1,0),sample(1.05,1)]
        definition={"kind":"r03_enemy_definition","scene":"Stage_3-1","detail":"id=1;family=Frog;owner=4;branch=Jump"}
        return r.compare([definition]+server,client)

    def test_compares_at_presentation_time(self):
        s=self.compare([sample(1.025,.5)])["Frog"]
        self.assertLess(s["maxPx"],.0001);self.assertEqual(s["checked"],1)

    def test_counts_wrong_approved_frame(self):
        self.assertEqual(self.compare([sample(1.025,.5,sprite="invented")])["Frog"]["wrongSprite"],1)

    def test_transition_is_not_silently_approved(self):
        s=self.compare([sample(1.025,.5)],[sample(1),sample(1.05,1,sprite="idle")])["Frog"]
        self.assertEqual(s["transitions"],1);self.assertIsNone(s["maxPx"])

    def test_missing_reference_and_gaps_are_counted(self):
        s=self.compare([sample(.8),sample(1.2)],[sample(1),sample(1.4)])["Frog"]
        self.assertEqual(s["gaps"],1);self.assertEqual(s["missingReference"],1)

    def test_pause_must_not_advance(self):
        s=self.compare([sample(1.01,paused=1),sample(1.02,.1,paused=1)])["Frog"]
        self.assertEqual(s["pausedMoved"],1)

    def test_scene_identity_is_required(self):
        self.assertEqual(self.compare([sample(1.025,.5,scene="Stage_3-5")]),{})

    def test_out_of_range_is_not_extrapolated(self):
        s=self.compare([sample(1.2)])["Frog"]
        self.assertEqual(s["missingReference"],1);self.assertEqual(s["checked"],0)

    def test_large_error_is_not_clamped(self):
        s=self.compare([sample(1.025,3)])["Frog"]
        self.assertGreater(s["maxPx"],2);self.assertEqual(s["above2Px"],1)

    def test_approved_hold_is_reported_separately_from_curve_error(self):
        row=sample(1.075,1)
        row["detail"]+=";state=1.05;interpolates=0;advancing=0"
        s=self.compare([row],[sample(1,0),sample(1.05,1),sample(1.1,2)])["Frog"]
        self.assertLess(s["maxPx"],.0001)
        self.assertEqual(s["heldSamples"],1)
        self.assertGreater(s["holdClockMaxPx"],7.9)
        self.assertAlmostEqual(s["heldMaxMs"],25)

    def test_a_wrong_position_is_not_excused_by_holding(self):
        row=sample(1.075,0)
        row["detail"]+=";state=1.05;interpolates=0;advancing=0"
        s=self.compare([row],[sample(1,0),sample(1.05,1),sample(1.1,2)])["Frog"]
        self.assertGreater(s["maxPx"],15.9);self.assertEqual(s["above2Px"],1)

    def test_unknown_sprite_is_inconclusive_not_a_match(self):
        s=self.compare([sample(1.025,.5,sprite="-2")])["Frog"]
        self.assertEqual(s["unknownSprite"],1);self.assertEqual(s["checked"],0)
