#!/usr/bin/env python3
"""
Set the platform owner's login (email and password) on a running Ecosys stack.

The API only creates the platform owner the first time it starts and never overwrites an
existing owner's credentials from .env afterwards. This script changes them in place, so you
keep all other data. It also writes the same values into .env, so a fresh database later gets
the same login.

Usage (from the repository root, with the stack started):

    python3 scripts/set_platform_owner.py
    python3 scripts/set_platform_owner.py --email you@example.com      # asks for the password
    python3 scripts/set_platform_owner.py --local                     # local trial compose files

Needs only Python 3.8+ and Docker Compose. Without Docker, pass --psql with a psql command
that reaches the database, e.g. --psql "psql -h localhost -U ecosys -d ecosys".
"""
from __future__ import annotations

import argparse
import base64
import getpass
import hashlib
import json
import os
import re
import secrets
import shlex
import struct
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

# PlatformConstants.SuperAdminUserId in the backend.
SUPER_ADMIN_USER_ID = "00000000-0000-0000-0000-000000000010"
MIN_PASSWORD_LENGTH = 12
# The API refuses these at startup (StartupSecurityValidation); refuse them here too.
DISALLOWED_PASSWORDS = {"superadmin123!", "changeme123!", "set_in_env", "changeme", "default"}
DISALLOWED_EMAILS = {"superadmin@ecosys.local", "admin@example.com", "set_in_env"}
EMAIL_PATTERN = re.compile(r"^[^@\s]+@[^@\s]+\.[^@\s]+$")


def hash_password(password: str) -> str:
    """ASP.NET Core Identity v3 hash: PBKDF2 HMAC-SHA512, 100,000 iterations, 16-byte salt, 32-byte key."""
    iterations, salt = 100_000, secrets.token_bytes(16)
    subkey = hashlib.pbkdf2_hmac("sha512", password.encode("utf-8"), salt, iterations, 32)
    # 0x01 format marker, then PRF (2 = HMACSHA512), iteration count and salt length, big-endian.
    header = b"\x01" + struct.pack(">III", 2, iterations, len(salt))
    return base64.b64encode(header + salt + subkey).decode("ascii")


def read_env(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    if not path.exists():
        return values
    for line in path.read_text(encoding="utf-8").splitlines():
        match = re.match(r"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$", line)
        if not match:
            continue
        value = match.group(2).strip()
        if value[:1] in "\"'" and value[-1:] == value[:1] and len(value) >= 2:
            value = value[1:-1]
        else:
            value = value.split(" #")[0].strip()
        values[match.group(1)] = value
    return values


def write_env_values(path: Path, updates: dict[str, str]) -> None:
    lines = path.read_text(encoding="utf-8").splitlines() if path.exists() else []
    remaining = dict(updates)
    for index, line in enumerate(lines):
        match = re.match(r"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=", line)
        if match and match.group(1) in remaining:
            key = match.group(1)
            lines[index] = f"{key}={remaining.pop(key)}"
    lines.extend(f"{key}={value}" for key, value in remaining.items())
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def fail(message: str) -> "NoReturn":  # type: ignore[name-defined]
    print(f"\nError: {message}", file=sys.stderr)
    sys.exit(1)


class DatabaseNotReady(Exception):
    pass


class Database:
    def __init__(self, base_command: list[str]):
        self.base_command = base_command

    def run(self, sql: str, variables: dict[str, str] | None = None, quiet: bool = False) -> str:
        command = [*self.base_command, "-v", "ON_ERROR_STOP=1", "-X", "-q", "-t", "-A"]
        for name, value in (variables or {}).items():
            command += ["-v", f"{name}={value}"]
        result = subprocess.run(command, input=sql, capture_output=True, text=True)
        if result.returncode != 0:
            if quiet:
                raise DatabaseNotReady(result.stderr.strip())
            fail(f"the database command failed:\n{result.stderr.strip() or result.stdout.strip()}")
        return result.stdout.strip()


def compose_command(files: list[str], env_file: Path) -> list[str]:
    env_args = ["--env-file", str(env_file)] if env_file.exists() else []
    for candidate in (["docker", "compose"], ["docker-compose"]):
        try:
            if subprocess.run([*candidate, "version"], capture_output=True).returncode == 0:
                return [*candidate, *env_args, *sum((["-f", f] for f in files), [])]
        except FileNotFoundError:
            continue
    fail("Docker Compose was not found. Install it, or pass --psql to reach the database directly.")


def wait_for_owner(db: Database, timeout_seconds: int) -> None:
    deadline = time.time() + timeout_seconds
    sql = 'SELECT count(*) FROM users WHERE "Id" = :\'id\';'
    while True:
        try:
            if db.run(sql, {"id": SUPER_ADMIN_USER_ID}, quiet=True) == "1":
                return
        except DatabaseNotReady as error:
            if time.time() > deadline:
                fail(f"the database is not reachable or not set up yet:\n{error}")
        if time.time() > deadline:
            fail("the platform owner account does not exist yet. Start the stack and wait for the API to finish starting, then run this again.")
        print("  waiting for the API to create the platform owner…")
        time.sleep(5)


def check_login(base_url: str, email: str, password: str) -> None:
    body = json.dumps({"emailOrUsername": email, "password": password}).encode("utf-8")
    request = urllib.request.Request(f"{base_url.rstrip('/')}/api/auth/login", data=body,
                                     headers={"Content-Type": "application/json"}, method="POST")
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            role = json.loads(response.read().decode("utf-8")).get("user", {}).get("role")
            print(f"  login check: OK (role {role})")
    except urllib.error.HTTPError as error:
        print(f"  login check: HTTP {error.code}. The credentials were saved; check the API log if sign-in fails.")
    except (urllib.error.URLError, OSError) as error:
        print(f"  login check skipped: could not reach {base_url} ({error}).")


def main() -> None:
    parser = argparse.ArgumentParser(description="Set the platform owner's email and password.")
    parser.add_argument("--email", help="platform owner email (asked for if omitted)")
    parser.add_argument("--password", help="password (asked for, hidden, if omitted; avoid on shared machines)")
    parser.add_argument("--full-name", help="display name (unchanged if omitted)")
    parser.add_argument("--env-file", default=".env", help="the .env to read and update (default .env)")
    parser.add_argument("--local", action="store_true", help="use docker-compose.local.yml as well (local trial)")
    parser.add_argument("--compose-file", action="append", help="compose file(s), repeatable (default docker-compose.yml)")
    parser.add_argument("--psql", help="psql command to use instead of the compose `db` service")
    parser.add_argument("--url", help="where to test the login (default ECOSYS_PUBLIC_URL from .env)")
    parser.add_argument("--no-env-update", action="store_true", help="do not write the values into .env")
    parser.add_argument("--wait", type=int, default=180, help="seconds to wait for the API to create the owner (default 180)")
    args = parser.parse_args()

    env_path = Path(args.env_file)
    env = read_env(env_path)

    email = (args.email or input(f"Platform owner email [{env.get('ECOSYS_PLATFORM_ADMIN_EMAIL', '')}]: ").strip()
             or env.get("ECOSYS_PLATFORM_ADMIN_EMAIL", "")).strip().lower()
    if not EMAIL_PATTERN.match(email) or email in DISALLOWED_EMAILS:
        fail(f"'{email}' is not a usable email address.")

    password = args.password
    if password is None:
        password = getpass.getpass("New password (hidden): ")
        if getpass.getpass("Repeat it: ") != password:
            fail("the passwords do not match.")
    if len(password) < MIN_PASSWORD_LENGTH:
        fail(f"the password must be at least {MIN_PASSWORD_LENGTH} characters.")
    if password.lower() in DISALLOWED_PASSWORDS:
        fail("that password is a known default; choose another.")
    if any(ch in password for ch in "\n\r\"'#"):
        fail("please avoid quotes, # and line breaks in the password (they break the .env file).")

    if args.psql:
        db = Database(shlex.split(args.psql))
    else:
        files = args.compose_file or ["docker-compose.yml"]
        if args.local and "docker-compose.local.yml" not in files:
            files.append("docker-compose.local.yml")
        for f in files:
            if not Path(f).exists():
                fail(f"{f} not found. Run this from the repository root (the folder with docker-compose.yml).")
        compose = compose_command(files, env_path)
        db_user = env.get("POSTGRES_USER") or "ecosys"
        db_name = env.get("POSTGRES_DB") or "ecosys"
        db = Database([*compose, "exec", "-T", "db", "psql", "-U", db_user, "-d", db_name])

    print("Checking the database…")
    wait_for_owner(db, args.wait)

    taken = db.run('SELECT count(*) FROM users WHERE lower("Email") = :\'email\' AND "Id" <> :\'id\';',
                   {"email": email, "id": SUPER_ADMIN_USER_ID})
    if taken != "0":
        fail(f"{email} already belongs to another user. Use a different email.")

    sets = ['"Email" = :\'email\'', '"PasswordHash" = :\'hash\'', '"IsActive" = true', '"MustChangePassword" = false',
            '"FailedLoginCount" = 0', '"LockedOutUntil" = NULL']
    variables = {"email": email, "hash": hash_password(password), "id": SUPER_ADMIN_USER_ID}
    if args.full_name:
        sets.append('"FullName" = :\'name\'')
        variables["name"] = args.full_name.strip()
    updated = db.run(f'UPDATE users SET {", ".join(sets)} WHERE "Id" = :\'id\' RETURNING "Email";', variables)
    if not updated:
        fail("no platform owner row was updated.")
    print(f"  platform owner is now {updated}")

    if not args.no_env_update:
        updates = {"ECOSYS_PLATFORM_ADMIN_EMAIL": email, "ECOSYS_PLATFORM_ADMIN_PASSWORD": password}
        if args.full_name:
            updates["ECOSYS_PLATFORM_ADMIN_FULL_NAME"] = f'"{args.full_name.strip()}"'
        write_env_values(env_path, updates)
        print(f"  {env_path} updated to match")

    base_url = args.url or env.get("ECOSYS_PUBLIC_URL") or f"http://localhost:{env.get('WEB_PORT', '8080')}"
    check_login(base_url, email, password)
    print(f"\nDone. Sign in at {base_url} as {email}.")


if __name__ == "__main__":
    main()
