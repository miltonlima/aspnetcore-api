CREATE TABLE IF NOT EXISTS education_classes (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    education_unit_id BIGINT NOT NULL,
    name VARCHAR(160) NOT NULL,
    code VARCHAR(60) NULL,
    academic_year VARCHAR(40) NULL,
    capacity INT NULL,
    description TEXT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_education_classes_unit FOREIGN KEY (education_unit_id)
        REFERENCES education_units(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT uq_education_classes_code UNIQUE (code)
);
