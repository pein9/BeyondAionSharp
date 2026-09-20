#!/bin/bash
# Bot-only, opt-in topology; never mounted by the deployment compose file.
set -eu
if [ "${AION_BOT_SECOND_GS:-false}" = "true" ]; then
    export MYSQL_PWD="${MYSQL_ROOT_PASSWORD}"
    mysql --protocol=socket -uroot -e 'CREATE DATABASE aion_gs2 CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;'
    mysql --protocol=socket -uroot aion_gs2 < /sql/game/aion_gs.sql
    mysql --protocol=socket -uroot -e 'CREATE DATABASE aion_cs2 CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;'
    mysql --protocol=socket -uroot aion_cs2 < /sql/chat/aion_cs.sql
    mysql --protocol=socket -uroot aion_ls -e "INSERT INTO gameservers (id, mask, password) VALUES (2, '*', '1234');"
    echo '[aion-bots] second game database and login registration ready'
fi
