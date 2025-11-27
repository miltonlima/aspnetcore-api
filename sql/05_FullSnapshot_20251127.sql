-- Snapshot generated on 2025-11-27 covering the current schema used by aspnetcore-api.
-- Execute against an empty schema to recreate the database structure prior to restoring data backups.

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

DROP TABLE IF EXISTS request_logs;
DROP TABLE IF EXISTS education_student_grades;
DROP TABLE IF EXISTS education_student_enrollments;
DROP TABLE IF EXISTS education_students;
DROP TABLE IF EXISTS education_classes;
DROP TABLE IF EXISTS education_units;
DROP TABLE IF EXISTS person_registrations;
DROP TABLE IF EXISTS users;

CREATE TABLE users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE person_registrations (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(150) NOT NULL,
    birth_date DATE NOT NULL,
    cpf CHAR(11) NOT NULL,
    email VARCHAR(180) NOT NULL,
    password VARCHAR(255) NOT NULL,
    description TEXT NULL,
    theme VARCHAR(20) NOT NULL DEFAULT 'dark',
    created_at DATETIME NOT NULL,
    UNIQUE KEY uq_person_registrations_cpf (cpf),
    UNIQUE KEY uq_person_registrations_email (email)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE education_units (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(160) NOT NULL,
    code VARCHAR(60) NOT NULL,
    city VARCHAR(160) NULL,
    state VARCHAR(80) NULL,
    description TEXT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_education_units_code UNIQUE (code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE education_classes (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    education_unit_id BIGINT NOT NULL,
    name VARCHAR(160) NOT NULL,
    code VARCHAR(60) NULL,
    academic_year VARCHAR(40) NULL,
    start_date DATE NULL,
    end_date DATE NULL,
    capacity INT NULL,
    description TEXT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_education_classes_unit FOREIGN KEY (education_unit_id)
        REFERENCES education_units(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT uq_education_classes_code UNIQUE (code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE education_students (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(160) NOT NULL,
    registration_code VARCHAR(80) NULL,
    cpf VARCHAR(11) NULL,
    birth_date DATE NULL,
    guardian_name VARCHAR(160) NULL,
    guardian_contact VARCHAR(160) NULL,
    notes TEXT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_education_students_registration UNIQUE (registration_code),
    CONSTRAINT uq_education_students_cpf UNIQUE (cpf)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE education_student_enrollments (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    student_id BIGINT NOT NULL,
    class_id BIGINT NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_student_enrollment_student FOREIGN KEY (student_id)
        REFERENCES education_students(id)
        ON DELETE CASCADE,
    CONSTRAINT fk_student_enrollment_class FOREIGN KEY (class_id)
        REFERENCES education_classes(id)
        ON DELETE CASCADE,
    CONSTRAINT uq_student_enrollment UNIQUE (student_id, class_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE education_student_grades (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    student_id BIGINT NOT NULL,
    class_id BIGINT NOT NULL,
    av1 DECIMAL(5,2) NULL,
    av2 DECIMAL(5,2) NULL,
    av3 DECIMAL(5,2) NULL,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_student_class (student_id, class_id),
    CONSTRAINT fk_grade_student FOREIGN KEY (student_id)
        REFERENCES education_students(id)
        ON DELETE CASCADE,
    CONSTRAINT fk_grade_class FOREIGN KEY (class_id)
        REFERENCES education_classes(id)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE request_logs (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    user_id BIGINT NULL,
    user_email VARCHAR(320) NULL,
    is_authenticated TINYINT(1) NOT NULL,
    method VARCHAR(16) NOT NULL,
    path VARCHAR(512) NOT NULL,
    query_string TEXT NULL,
    action VARCHAR(128) NULL,
    description VARCHAR(64) NULL,
    status_code INT NOT NULL,
    ip_address VARCHAR(128) NULL,
    user_agent TEXT NULL,
    duration_ms BIGINT NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_request_logs_created_at (created_at),
    INDEX idx_request_logs_user_id (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

SET FOREIGN_KEY_CHECKS = 1;
