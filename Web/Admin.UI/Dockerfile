# Build Stage: Angular App
FROM node:23-alpine AS build
WORKDIR /app
COPY . .
RUN npm install
RUN npm run build

# Runtime Stage: Node.js Server
FROM node:18-alpine AS runtime
WORKDIR /app

# Copy the Angular build output
COPY --from=build /app/dist/admin-ui/browser ./dist

# Copy the server files
COPY server ./server

# Install only server dependencies
WORKDIR /app/server
RUN npm ci

EXPOSE 80
CMD ["node", "main.js"]
