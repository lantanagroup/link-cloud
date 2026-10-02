# syntax=docker/dockerfile:1
# Build Stage: Angular App
FROM node:25-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN --mount=type=cache,id=npm,sharing=locked,target=/root/.npm \
    npm ci
COPY . .
RUN npm run build

# Runtime Stage: Node.js Server
FROM node:25-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production

COPY --from=build /app/dist/admin-ui/browser ./dist
COPY server/package.json server/package-lock.json ./server/
WORKDIR /app/server
RUN --mount=type=cache,id=npm,sharing=locked,target=/root/.npm \
    npm ci --omit=dev
COPY server/main.js server/blocked-paths.js ./

EXPOSE 80
CMD ["node", "main.js"]
