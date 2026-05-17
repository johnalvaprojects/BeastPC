-- Run in HeidiSQL on database `beastpc`
-- Adds a dedicated username column (separate from email)

ALTER TABLE `users`
  ADD COLUMN `username` VARCHAR(88) NULL AFTER `last_name`;

-- Backfill existing rows (adjust pattern if you prefer)
UPDATE `users`
SET `username` = CONCAT('user_', `id`)
WHERE `username` IS NULL OR `username` = '';

ALTER TABLE `users`
  MODIFY COLUMN `username` VARCHAR(88) NOT NULL;

ALTER TABLE `users`
  ADD UNIQUE INDEX `uq_users_username` (`username`);
