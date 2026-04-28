# Stage 1: Build
FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm ci

COPY . .
RUN npm run build

# Stage 2: Serve
FROM node:24-alpine AS runtime
RUN npm install -g serve

COPY --from=build /app/dist /app

EXPOSE 3000

CMD ["serve", "-s", "/app", "-l", "3000"]