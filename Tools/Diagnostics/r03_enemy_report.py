"""R03 actual-renderer comparison at the observer presentation clock.
No extrapolation: missing/long reference intervals and branch/visibility transitions
are counted explicitly. Paused views must remain fixed; they are not compared to
server-now. Definition identity is (scene, renderer id), never sprite name alone.
"""
import argparse
import bisect
from collections import defaultdict
import json
import math
from pathlib import Path


def detail(text):
    return dict(part.split("=", 1) for part in (text or "").split(";") if "=" in part)


def quantile(values, q):
    if not values:
        return None
    return sorted(values)[max(0, math.ceil(len(values)*q)-1)]


def read(path):
    with Path(path).open(encoding="utf-8-sig") as f:
        return [json.loads(line) for line in f if line.strip()]


def compare(server, client):
    definitions, tracks = {}, defaultdict(list)
    for row in server:
        d = detail(row.get("detail"))
        if "id" not in d:
            continue
        key = row.get("scene"), int(d["id"])
        if row.get("kind") == "r03_enemy_definition":
            definitions[key] = d
        elif row.get("kind") == "r03_enemy_sample":
            tracks[key].append((float(d["ptime"]), row, d))
    for samples in tracks.values():
        samples.sort(key=lambda x: x[0])
    times = {k:[x[0] for x in v] for k,v in tracks.items()}
    families = defaultdict(lambda:dict(errors=[], movingErrors=[], delays=[], candidates=[], spriteCandidates=[], checked=0, wrongSprite=0, gaps=0, transitions=0,
                                     waterChecked=0, waterActiveChecked=0, waterMismatch=0, pausedFrames=0, pausedMoved=0, hidden=0, missingReference=0, unknownSprite=0, heldSamples=0, heldMaxMs=0, holdClockMaxPx=0))
    paused = {}
    for row in client:
        if row.get("kind") != "r03_enemy_sample":
            continue
        d=detail(row.get("detail"));key=row.get("scene"),int(d["id"])
        if key not in definitions:
            continue
        stat=families[definitions[key]["family"]]
        pos=row["position"]
        if d.get("paused")=="1":
            stat["pausedFrames"]+=1
            if key in paused and math.hypot(pos["x"]-paused[key]["x"],pos["y"]-paused[key]["y"])>.0001:
                stat["pausedMoved"]+=1
            paused[key]=pos
            continue
        paused.pop(key,None)
        if d.get("visible")!="1":
            stat["hidden"]+=1
            continue
        clock=float(d["ptime"]);t=float(d.get("state",clock));samples=tracks[key]
        if d.get("interpolates")=="0" and clock>t+.00001:
            stat["heldSamples"]+=1;stat["heldMaxMs"]=max(stat["heldMaxMs"],(clock-t)*1000)
            ci=bisect.bisect_right(times[key],clock)-1
            if 0<=ci<len(samples)-1:
                ca,cr,cd=samples[ci];cb,nr,nd=samples[ci+1]
                if cb>ca and cb-ca<=.1 and cd["visible"]==nd["visible"]=="1":
                    cv=(clock-ca)/(cb-ca)
                    cx=cr["position"]["x"]+(nr["position"]["x"]-cr["position"]["x"])*cv
                    cy=cr["position"]["y"]+(nr["position"]["y"]-cr["position"]["y"])*cv
                    stat["holdClockMaxPx"]=max(stat["holdClockMaxPx"],math.hypot(pos["x"]-cx,pos["y"]-cy)*16)
        i=bisect.bisect_right(times[key],t)-1
        if i<0 or i+1>=len(samples):
            stat["missingReference"]+=1
            continue
        a,ar,ad=samples[i];b,br,bd=samples[i+1]
        if b-a>.1 or b<=a:
            stat["gaps"]+=1
            continue
        # Visibility/frame boundaries are discrete. A reference only brackets the edge;
        # do not call an unknown position on that frame a successful measurement.
        if ad["visible"]!="1" or bd["visible"]!="1" or ad["sprite"]!=bd["sprite"]:
            stat["transitions"]+=1
            continue
        if "-2" in (ad["sprite"],bd["sprite"],d["sprite"]):
            stat["unknownSprite"]+=1
            continue
        v=(t-a)/(b-a)
        if "water" in ad and ad.get("water")==bd.get("water"):
            stat["waterChecked"]+=1
            stat["waterActiveChecked"]+=int(ad["water"]=="1")
            mismatch=d.get("water")!=ad["water"]
            if ad["water"]=="1" and d.get("water")=="1":
                for field in ("surface","line","useLine","premultiply"):
                    expected=float(ad[field])+(float(bd[field])-float(ad[field]))*v
                    mismatch=mismatch or abs(float(d[field])-expected)>.001
            stat["waterMismatch"]+=int(mismatch)
        x=ar["position"]["x"]+(br["position"]["x"]-ar["position"]["x"])*v
        y=ar["position"]["y"]+(br["position"]["y"]-ar["position"]["y"])*v
        error=math.hypot(pos["x"]-x,pos["y"]-y)*16
        if error>2 and len(stat["candidates"])<20:
            stat["candidates"].append(dict(id=key[1],branch=definitions[key].get("branch"),time=t,frame=row.get("frame"),
                errorPx=error,actual=pos,expected={"x":x,"y":y},fromTime=a,toTime=b,fromPosition=ar["position"],toPosition=br["position"]))
        stat["errors"].append(error);stat["checked"]+=1
        if math.hypot(br["position"]["x"]-ar["position"]["x"],br["position"]["y"]-ar["position"]["y"])>.00001:
            stat["movingErrors"].append(error)
            stat["delays"].append((row.get("timeline",t)+row.get("bufferMs",0)/1000+row.get("rttMs",0)/2000-t)*1000)
        pose_time=clock if d.get("advancing")=="1" else t
        pi=bisect.bisect_right(times[key],pose_time)-1
        if 0<=pi<len(samples)-1:
            pa,pr,pd=samples[pi];pb,pnr,pnd=samples[pi+1]
            if pb-pa<=.1 and pd["visible"]==pnd["visible"]=="1" and pd["sprite"]==pnd["sprite"]:
                if d["sprite"]!=pd["sprite"]:
                    stat["wrongSprite"]+=1
                    if len(stat["spriteCandidates"])<20:
                        stat["spriteCandidates"].append(dict(id=key[1],time=pose_time,actual=d["sprite"],expected=pd["sprite"],fromTime=pa,toTime=pb,frame=row.get("frame")))
    result={}
    for name,s in families.items():
        values=s.pop("errors");moving=s.pop("movingErrors");delays=s.pop("delays")
        result[name]=dict(s,movingChecked=len(moving),movingMaxPx=max(moving,default=None),movingP95Px=quantile(moving,.95),
                          delayP95Ms=quantile(delays,.95),maxPx=max(values,default=None),p95Px=quantile(values,.95),
                          above2Px=sum(x>2 for x in values))
    return result


def native_curves(rows):
    groups=defaultdict(list)
    for row in rows:
        if row.get("kind")=="r03_native_curve":
            d=detail(row.get("detail"));groups[d["family"]].append(d)
    return {family:dict(samples=len(values),progressMin=min(float(v["progress"]) for v in values),
                        progressMax=max(float(v["progress"]) for v in values),
                        groundP95Px=quantile([float(v["groundErrorPx"]) for v in values],.95),
                        groundMaxPx=max(float(v["groundErrorPx"]) for v in values),
                        liftP95Px=quantile([float(v["liftErrorPx"]) for v in values],.95),
                        liftMaxPx=max(float(v["liftErrorPx"]) for v in values)) for family,values in groups.items()}


def capture_health(rows):
    dropped=max((row.get("dropped",0) for row in rows),default=0)
    capped=any(row.get("kind")=="capture_limit" for row in rows)
    return dict(valid=bool(rows) and not dropped and not capped,rows=len(rows),dropped=dropped,capped=capped)


def death_cleanup(server, clients):
    definitions={}
    deaths={}
    for row in server:
        d=detail(row.get("detail"))
        if row.get("kind")=="r03_enemy_definition":
            definitions[(row.get("scene"),d["id"])]=d
        elif row.get("kind")=="r03_enemy_sample" and d.get("visible")=="1":
            info=definitions.get((row.get("scene"),d["id"]))
            if info and info["branch"].lower()=="death":
                key=(row.get("scene"),info["owner"])
                deaths.setdefault(key,dict(family=info["family"],time=float(d["ptime"])))
    result={}
    shadow_ends={}
    for row in server:
        if row.get("kind")!="r03_enemy_sample":continue
        d=detail(row.get("detail"));info=definitions.get((row.get("scene"),d["id"]))
        if d.get("visible")=="1" and info and "shadow" in info["branch"].lower():
            key=(row.get("scene"),info["owner"])
            shadow_ends[key]=max(shadow_ends.get(key,float("-inf")),float(d["ptime"]))
    for name,rows in clients.items():
        actors={key:dict(owner=key[1],scene=key[0],family=d["family"],authoritativeDeathPoseAt=d["time"],
                        authoritativeLastShadowAt=shadow_ends.get(key),shadowBeyondAuthorityMs=None,
                        deathVisibleSamples=0,shadowVisibleSamples=0,lastShadowAt=None,maxShadowAfterDeathMs=0)
                for key,d in deaths.items()}
        for row in rows:
            if row.get("kind")!="r03_enemy_sample":continue
            d=detail(row.get("detail"))
            if d.get("visible")!="1":continue
            info=definitions.get((row.get("scene"),d["id"]))
            if not info:continue
            actor=actors.get((row.get("scene"),info["owner"]))
            if actor is None:continue
            if info["branch"].lower()=="death":actor["deathVisibleSamples"]+=1
            if "shadow" in info["branch"].lower():
                at=float(d["ptime"]);actor["shadowVisibleSamples"]+=1
                actor["lastShadowAt"]=max(at,actor["lastShadowAt"] if actor["lastShadowAt"] is not None else at)
                actor["maxShadowAfterDeathMs"]=max(actor["maxShadowAfterDeathMs"],(at-actor["authoritativeDeathPoseAt"])*1000)
                if actor["authoritativeLastShadowAt"] is not None:
                    actor["shadowBeyondAuthorityMs"]=max(actor["shadowBeyondAuthorityMs"] or 0,(at-actor["authoritativeLastShadowAt"])*1000)
        result[name]=list(actors.values())
    return result


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("folder",type=Path);p.add_argument("--output",type=Path,required=True)
    p.add_argument("--offline",type=Path,help="Native offline capture from the same stage and build")
    a=p.parse_args();server=read(a.folder/"server-motion.jsonl")
    clients={c.stem:read(c) for c in sorted(a.folder.glob("client-*-motion.jsonl"))}
    result={name:compare(server,rows) for name,rows in clients.items()}
    report={"clients":result,"limits":{"p95Px":1,"maxPx":2},
            "deathCleanup":death_cleanup(server,clients),
            "captureHealth":{"server":capture_health(server),**{name:capture_health(rows) for name,rows in clients.items()}},
            "note":"Transition/gap frames remain unverified. No human acceptance is inferred."}
    if a.offline:
        offline=read(a.offline)
        report["captureHealth"]["offline"]=capture_health(offline)
        report["nativeReference"]={"server":native_curves(server),"offline":native_curves(offline),
            "note":"Independent native runs, not identical RNG replays. Ideal unobstructed curves do not model collision/grid corrections. Observer errors above compare actual server renderers, not the ideal formula."}
    a.output.write_text(json.dumps(report,indent=2),encoding="utf-8")
    print(json.dumps(result,indent=2))
    if not all(health["valid"] for health in report["captureHealth"].values()):
        raise SystemExit(2)
    if not clients or not all(result.values()):
        raise SystemExit(3)


if __name__=="__main__":
    main()
