CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(100) NOT NULL UNIQUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX idx_users_username ON users (username);

CREATE PROCEDURE deactivate_user(user_id INT)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE users SET is_active = FALSE WHERE id = user_id;
END;
$$;
