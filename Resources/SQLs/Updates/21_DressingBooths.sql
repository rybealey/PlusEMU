-- Changing booths (Builders > Corporations > Clothing) become dressing booths:
-- standing in one auto-opens the Change Your Looks window (closes on step-off).
-- Previous value was 'pressure_tile', which the emulator never mapped (fell
-- back to None / generic switch).
-- Idempotent: safe to re-run.

UPDATE furniture SET interaction_type = 'dressing_booth'
WHERE item_name IN ('boutique_changing1', 'boutique_changing2', 'boutique_changing3');
