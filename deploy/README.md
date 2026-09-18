# Deploying ChaldeaBot to sparkle (pull-based)

GitHub Actions never touches sparkle over SSH. On every push to `master` or a
`v*` tag, `.github/workflows/build.yml` builds one container image and pushes
it to GHCR (`ghcr.io/fuji97/chaldea-user-bot`) under a moving channel tag:

| Trigger                | Channel tag | Stack   |
|-------------------------|-------------|---------|
| push to `master`        | `beta`      | Test    |
| push tag `v*`            | `stable`    | Prod    |

A [Watchtower](https://containrrr.dev/watchtower/) agent running on sparkle
polls GHCR every 5 minutes and recreates any container labelled
`com.centurylinklabs.watchtower.enable=true` whose followed tag now points at
a new digest. Deploy latency after a push completes is therefore up to 5
minutes; there is no faster pull-based path, and that is an accepted
tradeoff.

Two independent stacks run side by side on sparkle:

- **Prod** — `chaldeabot` bot, existing live database, tracks `stable`.
- **Test** — `chaldeabot_beta` bot, separate token/database/webhook path,
  tracks `beta`.

## One-time setup, GitHub side

After the first successful push to `master` produces an image:

1. GitHub → `fuji97/chaldea-user-bot` → **Packages** → `chaldea-user-bot` →
   **Package settings** → **Change visibility** → **Public**.

   Required so Watchtower and `docker pull` on sparkle can pull without
   registry credentials. If the package must stay private instead, run
   `docker login ghcr.io` on sparkle with a `read:packages` PAT and mount an
   equivalent `/root/.docker/config.json` into the Watchtower container via
   `WATCHTOWER_REGISTRY_AUTH`-compatible configuration.

## One-time setup, sparkle

```sh
git clone https://github.com/fuji97/chaldea-user-bot.git
cd chaldea-user-bot
# Later updates:
# git pull --ff-only

cp deploy/.env.prod.example deploy/.env.prod
cp deploy/.env.beta.example deploy/.env.beta
# fill every value in both files, then:
chmod 600 deploy/.env.prod deploy/.env.beta

# Docker objects referenced by the stack must pre-exist.
docker network inspect pangolin
docker volume create chaldeabot_beta_dbdata

# start the host-wide updater once
docker compose -f deploy/watchtower/docker-compose.yml up -d
```

## Prod cutover (data-preserving)

Do this once, in order, to move the live `chaldeabot` stack onto the new
compose file without losing its Postgres data.

1. Find the live Postgres volume name and put it in `deploy/.env.prod` as
   `DB_VOLUME`. On sparkle the active volume is `chaldeabot_dbdata_16`:

   ```sh
   docker volume ls
   ```

2. Stop the old stack **without** removing volumes:

   ```sh
   docker compose -f docker-compose.yml -f docker-compose.production.yml down
   # or, if the old compose files are already gone after the pull:
   docker stop chaldeabot_bot chaldeabot_db && docker rm chaldeabot_bot chaldeabot_db
   ```

   Never pass `-v` here — that deletes the volume.

3. Start the new stack:

   ```sh
   docker compose -f deploy/docker-compose.yml --env-file deploy/.env.prod up -d
   ```

4. Confirm it came up in webhook mode against the preserved data:

   ```sh
   docker logs -f chaldeabot_bot
   # expect: Listening on bot endpoint /telegram/chaldeabot in webhook mode
   ```

If the live database turns out not to live in a Docker volume, dump and
restore instead of reusing a volume:

```sh
docker exec chaldeabot_db pg_dump -U chaldeabot chaldeabot > prod.sql
docker volume create chaldeabot_dbdata
# start the new stack, then:
docker exec -i chaldeabot_db psql -U chaldeabot chaldeabot < prod.sql
```

## Test stack

```sh
docker compose -f deploy/docker-compose.yml --env-file deploy/.env.beta up -d
```

## Reverse proxy (Pangolin) routing

Pangolin on sparkle runs containerised. Both bot services join its existing
external `pangolin` Docker network, so no host TCP port is published.

- Keep the prod target unchanged: `chaldeabot_bot:5000` with prefix
  `/telegram/chaldeabot`.
- Add a second target for the beta path:
  `chaldeabot_beta_bot:5000` with prefix `/telegram/chaldeabot_beta`.

`PANGOLIN_NETWORK=pangolin` in each environment file is the shared external
network name. If the host later uses a different network name, change that
variable only; the bot targets remain their `${STACK}_bot:5000` container DNS
names.

## Release / rollback

No SSH access is required for any of these — run from a workstation with
`git` and, for the rollback command, a `docker login ghcr.io` session:

- **Release prod:**

  ```sh
  git tag v1.2.0 && git push origin v1.2.0
  ```

  Actions repoints the `stable` tag at the new image; Watchtower recreates
  `chaldeabot_bot` on sparkle within 5 minutes.

- **Rollback prod** — repoint `stable` at a known-good previous version
  without waiting for a new build:

  ```sh
  docker buildx imagetools create --tag ghcr.io/fuji97/chaldea-user-bot:stable \
    ghcr.io/fuji97/chaldea-user-bot:1.1.0
  ```

  Watchtower rolls the running container back on its next poll.

- **Force an immediate poll on sparkle** instead of waiting up to 5 minutes:

  ```sh
  docker restart watchtower
  ```

## Migrations

Every container start runs with `MIGRATE=true`, so `Database.MigrateAsync`
applies any pending EF migration shipped in a release before the bot starts
serving traffic. `SEED=false` in both environments — seed data is not
re-applied on every deploy.

## Verifying a deploy

- **Test:**

  ```sh
  docker logs chaldeabot_beta_bot
  # expect: Listening on bot endpoint /telegram/chaldeabot_beta in webhook mode
  curl -s "https://api.telegram.org/bot<BETA_TOKEN>/getWebhookInfo"
  # expect: "url" ends in /telegram/chaldeabot_beta, no last_error_message
  ```

  Then send `/help` to the beta bot in Telegram and confirm it replies.

- **Prod after cutover:**

  ```sh
  docker ps   # chaldeabot_bot running ghcr.io/fuji97/chaldea-user-bot:stable
  curl -s "https://api.telegram.org/bot<PROD_TOKEN>/getWebhookInfo"
  # expect: "url" still ends in /telegram/chaldeabot
  ```

  Then send `/list` in a private chat to the prod bot and confirm previously
  registered Master names are still present — this proves the external
  volume carried the data across the cutover.

- **Auto-update:** push a trivial commit to `master`, wait up to 5 minutes,
  then:

  ```sh
  docker inspect --format '{{.Image}}' chaldeabot_beta_bot   # image id changed
  docker logs watchtower                                      # records the update
  ```
