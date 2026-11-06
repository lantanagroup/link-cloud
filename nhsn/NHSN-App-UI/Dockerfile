FROM node:26.5.0-alpine AS build
WORKDIR /app
COPY packages/* ./packages/
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build:app

FROM node:26.5.0-alpine AS runtime
WORKDIR /app
COPY server ./server
RUN npm install --prefix ./server --omit=dev
COPY --from=build /app/dist ./dist

EXPOSE 8080
CMD ["node", "server/main.js"]