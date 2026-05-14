CREATE TABLE IF NOT EXISTS "Feedbacks" (
    "Id" SERIAL PRIMARY KEY,
    "StudentNumber" VARCHAR(20) NOT NULL,
    "Message" VARCHAR(500) NOT NULL,
    "Date" TIMESTAMP NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_Feedbacks_StudentNumber" ON "Feedbacks" ("StudentNumber");
