CREATE TABLE Boards (
    Id         TEXT PRIMARY KEY,
    Name       TEXT NOT NULL,
    SortOrder  INTEGER NOT NULL,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL
);

CREATE TABLE OriginalFiles (
    Hash        TEXT PRIMARY KEY,
    FilePath    TEXT NOT NULL,
    SizeBytes   INTEGER NOT NULL,
    ImportedUtc TEXT NOT NULL,
    RefCount    INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE Clips (
    Id                    TEXT PRIMARY KEY,
    BoardId               TEXT NOT NULL REFERENCES Boards(Id) ON DELETE CASCADE,
    Name                  TEXT NOT NULL,
    OriginalFileHash      TEXT NOT NULL REFERENCES OriginalFiles(Hash),
    OriginalFileExtension TEXT NOT NULL,
    OriginalDurationMs    INTEGER NOT NULL,
    SortOrder             INTEGER NOT NULL,
    ColorTag              TEXT NULL,
    CreatedUtc            TEXT NOT NULL,
    UpdatedUtc            TEXT NOT NULL
);
CREATE INDEX IX_Clips_BoardId ON Clips(BoardId);

CREATE TABLE ClipEditSettings (
    ClipId                   TEXT PRIMARY KEY REFERENCES Clips(Id) ON DELETE CASCADE,
    TrimStartMs              INTEGER NOT NULL DEFAULT 0,
    TrimEndMs                INTEGER NULL,
    PitchSemitones           REAL NOT NULL DEFAULT 0,
    GainDb                   REAL NOT NULL DEFAULT 0,
    FadeInMs                 INTEGER NOT NULL DEFAULT 0,
    FadeOutMs                INTEGER NOT NULL DEFAULT 0,
    CurrentRenderKey         TEXT NULL,
    PttEnabled               INTEGER NOT NULL DEFAULT 0,
    PttKeyOverride           INTEGER NULL,
    PttBeginDelayMsOverride  INTEGER NULL,
    PttEndDelayMsOverride    INTEGER NULL,
    UpdatedUtc               TEXT NOT NULL
);

CREATE TABLE AppSettings (
    Key   TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
