# Frontend Dockerfile
FROM node:20-alpine AS build
WORKDIR /app

# Install dependencies
COPY package*.json ./
RUN npm config set fetch-retries 5 && npm config set fetch-retry-mintimeout 20000 && npm config set fetch-retry-maxtimeout 120000
RUN npm install --legacy-peer-deps

# Copy source and build
COPY . .
RUN npm run build

# Production stage
FROM node:20-alpine AS production
WORKDIR /app

# Copy built application
COPY --from=build /app/dist/AngularFrontEnd ./dist/AngularFrontEnd
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/package*.json ./

EXPOSE 4200

# Run the Angular SSR server
CMD ["node", "dist/AngularFrontEnd/server/server.mjs"]
