# Production image: builds the static site with Node, then serves it with
# nginx. No Node at runtime and no development server.
#
#   docker build --build-arg VITE_API_BASE_URL=https://<api host> -t idtheathlete-frontend frontend
#   docker run -p 5174:8080 idtheathlete-frontend
#
# VITE_API_BASE_URL is the backend's origin, inlined into the bundle at
# build time (see src/api/client.ts), so changing it means rebuilding. The
# backend must list this site's origin in AllowedOrigins.

# Keep in step with .nvmrc.
ARG NODE_VERSION=24

FROM node:${NODE_VERSION}-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
RUN npm run build

# Runs as a non-root user. nginx fills /etc/nginx/templates/*.template from
# environment variables at startup, so a host that assigns PORT is honoured.
FROM nginxinc/nginx-unprivileged:1.30-alpine
ENV PORT=8080
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
