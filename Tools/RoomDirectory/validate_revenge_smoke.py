"""Strict checks for the Development two-client Revenge Kart input smoke."""
import math
import re
import statistics


def validate_revenge_capture(rows, server=False):
    if server:
        owners = {row.get('player') for row in rows if row.get('kind') == 'revenge_cart_authority' and row.get('held', 0) & 64}
        if len(owners) != 2:
            raise RuntimeError('missing authoritative C boost inputs from two owners')
        return dict(boostOwners=sorted(owners))
    views = {row.get('player') for row in rows if row.get('kind') == 'revenge_cart_present'}
    if len(views) != 2:
        raise RuntimeError('client did not present both Revenge Karts')
    inputs, visuals, confirms = {}, {}, {}
    for row in rows:
        kind = row.get('kind', '')
        if not kind.startswith('revenge_launch_'):
            continue
        if kind in ('revenge_launch_reject', 'revenge_launch_timeout'):
            raise RuntimeError('charged release was rejected or timed out')
        match = re.search(r'\bcart=(-?\d+);generation=(\d+)', row.get('detail', ''))
        tick = row.get('inputTick')
        if not match or type(tick) is not int or not 0 <= tick <= 0xffffffff:
            raise RuntimeError('missing release correlation')
        key = (row.get('player'), *match.groups(), tick)
        if kind == 'revenge_launch_input':
            if key in inputs:
                raise RuntimeError('duplicate release input')
            inputs[key] = row
        elif kind in ('revenge_launch_first_visual', 'revenge_launch_confirm'):
            duration = row.get('durationMs')
            if key not in inputs or type(duration) not in (int, float) or not math.isfinite(duration) or duration < 0:
                raise RuntimeError('uncorrelated or invalid launch response')
            target = visuals if kind.endswith('first_visual') else confirms
            if key in target:
                raise RuntimeError('duplicate launch response')
            target[key] = duration
    if len(inputs) != 3 or inputs.keys() != visuals.keys() or inputs.keys() != confirms.keys():
        raise RuntimeError('expected three releases, each with first visual and confirmation')
    if len({key[0] for key in inputs}) != 1:
        raise RuntimeError('owner release metrics mixed between players')
    return dict(presentedPlayers=sorted(views), confirmedReleases=len(confirms),
                firstVisualMs=list(visuals.values()), confirmationMs=list(confirms.values()),
                medianFirstVisualMs=statistics.median(visuals.values()),
                medianConfirmationMs=statistics.median(confirms.values()))
