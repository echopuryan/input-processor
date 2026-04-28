#!/bin/sh
set -e

# create the .htpasswd
touch /etc/nginx/.htpasswd

if [ -n "$AUTH_USERS" ]; then
    echo "$AUTH_USERS" | tr ',' '\n' | while IFS=':' read -r user pass; do
        if [ -n "$user" ] && [ -n "$pass" ]; then
            echo "Creating credentials for user: $user"
            htpasswd -Bb /etc/nginx/.htpasswd "$user" "$pass"
        fi
    done
fi

echo "Starting Nginx..."
exec nginx -g "daemon off;"