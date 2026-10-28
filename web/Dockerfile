# syntax=docker/dockerfile:1.7

# Stage 1: Build & Bundle
FROM node:22-alpine AS build
WORKDIR /app

# Install dependencies with locked versions
COPY package.json package-lock.json ./
RUN npm ci

# Copy web source and compile Nuxt 4 production bundle
COPY . .
ENV NODE_ENV=production
RUN npm run build

# Stage 2: Hardened Runtime Container
FROM node:22-alpine AS final
WORKDIR /app

ENV NODE_ENV=production \
    PORT=3000 \
    HOST=0.0.0.0

# Run as non-root user 'node' (UID 1000 in alpine)
USER node

# Copy compiled Nitro server output
COPY --from=build --chown=node:node /app/.output ./.output

EXPOSE 3000

ENTRYPOINT ["node", ".output/server/index.mjs"]
