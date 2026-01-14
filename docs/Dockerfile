## Stage 1: Build the app
FROM node:lts AS build

WORKDIR /app

ARG EVENTCATALOG_STARTER_LICENSE_KEY

ENV EVENTCATALOG_STARTER_LICENSE_KEY=$EVENTCATALOG_STARTER_LICENSE_KEY

# Install dependencies
COPY package.json package-lock.json ./

RUN npm ci

# This seems to be needed to perform the build command below in a linux distro
RUN npm i @rollup/rollup-linux-x64-gnu

# Copy source code
COPY . .

# Fix for Astro in Docker: https://github.com/withastro/astro/issues/2596
ENV NODE_OPTIONS=--max_old_space_size=2048
# Build the app
RUN npm run build

## Stage 2: Serve app with httpd server
FROM httpd:2.4

# Copy built app to serve
COPY --from=build /app/dist /usr/local/apache2/htdocs