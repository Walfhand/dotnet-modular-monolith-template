.PHONY: up-all up-backend up-db down

up-all:
	docker compose up --build

up-backend:
	docker compose up --build api

up-db:
	docker compose up database

down:
	docker compose down
