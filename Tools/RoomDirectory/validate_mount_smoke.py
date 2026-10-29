"""Validate owner-side riding evidence, independently of server checkpoints."""


def validate_mount_capture(rows):
    result = {}
    owner = [row for row in rows if row.get('localPlayer')]
    for arc, minimum in (('Mount', 3), ('Dismount', 4)):
        events = [row for row in owner if 'arc=' + arc in row.get('detail', '').split(';')]
        predicted = sum(row.get('kind') == 'riding_prediction_begin' and
                        row.get('detail', '').startswith('gesture_only;') for row in events)
        confirmed = sum(row.get('kind') == 'riding_prediction_confirm' and
                        row.get('detail', '').startswith('phase_preserved;') for row in events)
        rejected = [row.get('detail') for row in events if row.get('kind') == 'riding_prediction_end' and
                    not row.get('detail', '').startswith('authority_released;')]
        if predicted < minimum or confirmed != predicted or rejected:
            raise ValueError(f'{arc}: predicted={predicted}, confirmed={confirmed}, rejected={rejected}')
        result[arc] = dict(predicted=predicted, confirmedPreservingPhase=confirmed)
    return result
