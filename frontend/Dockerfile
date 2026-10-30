# syntax=docker/dockerfile:1
#
# Build context: the frontend/ directory.
# The React build calls the API on its own origin (/api, /media); nginx forwards those
# requests to API_INTERNAL_URL, so no API address or secret is baked into the bundle.

# ---- Build ------------------------------------------------------------------
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# ---- Serve (non-root nginx) -------------------------------------------------
FROM nginxinc/nginx-unprivileged:stable-alpine

# The image renders /etc/nginx/templates/*.template into conf.d at startup.
# The stock default.conf is removed so only ours is served.
RUN rm -f /etc/nginx/conf.d/default.conf
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist /usr/share/nginx/html

ENV PORT=8080 \
    API_INTERNAL_URL=http://api:8080 \
    NGINX_TRUST_X_REAL_IP=off \
    NGINX_ENTRYPOINT_LOCAL_RESOLVERS=1 \
    NGINX_ENVSUBST_FILTER="^(PORT|API_INTERNAL_URL|NGINX_LOCAL_RESOLVERS|NGINX_TRUST_X_REAL_IP)$"

EXPOSE 8080
