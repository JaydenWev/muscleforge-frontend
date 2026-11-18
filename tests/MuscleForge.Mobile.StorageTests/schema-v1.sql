CREATE TABLE local_templates (
    server TEXT NOT NULL,
    account_id TEXT NOT NULL,
    id TEXT NOT NULL,
    name TEXT NOT NULL,
    archived_at TEXT NULL,
    PRIMARY KEY(server, account_id, id)
);
CREATE TABLE local_template_exercises (
    server TEXT NOT NULL,
    account_id TEXT NOT NULL,
    template_id TEXT NOT NULL,
    id TEXT NOT NULL,
    position INTEGER NOT NULL CHECK(typeof(position) = 'integer' AND position BETWEEN 0 AND 99),
    exercise_id TEXT NOT NULL,
    name TEXT NOT NULL,
    equipment TEXT NOT NULL,
    tracking INTEGER NOT NULL CHECK(typeof(tracking) = 'integer' AND tracking = 0),
    load_convention INTEGER NOT NULL CHECK(typeof(load_convention) = 'integer' AND load_convention IN (0, 1)),
    is_archived INTEGER NOT NULL CHECK(typeof(is_archived) = 'integer' AND is_archived IN (0, 1)),
    rest_seconds INTEGER NULL CHECK(rest_seconds IS NULL OR (typeof(rest_seconds) = 'integer' AND rest_seconds BETWEEN 0 AND 86400)),
    PRIMARY KEY(server, account_id, template_id, id),
    UNIQUE(server, account_id, template_id, position),
    FOREIGN KEY(server, account_id, template_id) REFERENCES local_templates(server, account_id, id) ON DELETE CASCADE
);
CREATE TABLE local_template_sets (
    server TEXT NOT NULL,
    account_id TEXT NOT NULL,
    template_id TEXT NOT NULL,
    template_exercise_id TEXT NOT NULL,
    id TEXT NOT NULL,
    position INTEGER NOT NULL CHECK(typeof(position) = 'integer' AND position BETWEEN 0 AND 99),
    kind INTEGER NOT NULL CHECK(typeof(kind) = 'integer' AND kind IN (0, 1)),
    rep_min INTEGER NULL,
    rep_max INTEGER NULL,
    weight_thousandths INTEGER NULL CHECK(weight_thousandths IS NULL OR (typeof(weight_thousandths) = 'integer' AND weight_thousandths BETWEEN 0 AND 9999999999)),
    CHECK((rep_min IS NULL AND rep_max IS NULL) OR
          (typeof(rep_min) = 'integer' AND typeof(rep_max) = 'integer' AND rep_min BETWEEN 1 AND 2147483647 AND rep_max BETWEEN rep_min AND 2147483647)),
    PRIMARY KEY(server, account_id, template_id, template_exercise_id, id),
    UNIQUE(server, account_id, template_id, id),
    UNIQUE(server, account_id, template_id, template_exercise_id, position),
    FOREIGN KEY(server, account_id, template_id, template_exercise_id) REFERENCES local_template_exercises(server, account_id, template_id, id) ON DELETE CASCADE
);
