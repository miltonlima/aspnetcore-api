CREATE TABLE IF NOT EXISTS education_units (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(160) NOT NULL,
    code VARCHAR(60) NOT NULL,
    city VARCHAR(160) NULL,
    state VARCHAR(80) NULL,
    description TEXT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_education_units_code UNIQUE (code)
);
