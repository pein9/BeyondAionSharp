-- Bot-only identities. This file is mounted only by docker-compose.bots.yml.
USE `aion_ls`;

-- Keep the game-server registration deterministic even if the shared seed changes.
INSERT INTO `gameservers` (`id`, `mask`, `password`)
VALUES (1, '*', '1234')
ON DUPLICATE KEY UPDATE `mask` = VALUES(`mask`), `password` = VALUES(`password`);

-- SHA-1 + Base64 of "aion-bots", matching Java AccountUtils.encodePassword.
INSERT INTO `account_data`
	(`name`, `password`, `activated`, `access_level`, `membership`, `old_membership`, `last_server`, `last_mac`)
VALUES
	('director', 'Zd2bHPtGKgR+5Xk+9H2ugwisL08=', TRUE, 9, 0, 0, 1, 'xx-xx-xx-xx-xx-xx')
ON DUPLICATE KEY UPDATE
	`password` = VALUES(`password`),
	`activated` = VALUES(`activated`),
	`access_level` = VALUES(`access_level`),
	`membership` = VALUES(`membership`),
	`old_membership` = VALUES(`old_membership`),
	`last_server` = VALUES(`last_server`),
	`last_mac` = VALUES(`last_mac`);

INSERT INTO `account_time` (`account_id`)
SELECT `id` FROM `account_data` WHERE `name` = 'director'
ON DUPLICATE KEY UPDATE `account_id` = VALUES(`account_id`);
