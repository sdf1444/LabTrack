CREATE OR REPLACE FUNCTION prevent_sample_event_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'Sample event history is append-only'
        USING ERRCODE = '55000';
END;
$$;

DROP TRIGGER IF EXISTS trg_events_append_only ON "Events";
CREATE TRIGGER trg_events_append_only
BEFORE UPDATE OR DELETE ON "Events"
FOR EACH ROW EXECUTE FUNCTION prevent_sample_event_mutation();
ALTER TABLE "Events" ENABLE ALWAYS TRIGGER trg_events_append_only;

DROP TRIGGER IF EXISTS trg_events_no_truncate ON "Events";
CREATE TRIGGER trg_events_no_truncate
BEFORE TRUNCATE ON "Events"
FOR EACH STATEMENT EXECUTE FUNCTION prevent_sample_event_mutation();
ALTER TABLE "Events" ENABLE ALWAYS TRIGGER trg_events_no_truncate;
