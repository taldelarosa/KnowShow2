-- Check Criminal Minds subtitle import status
-- Run with: sqlite3 production_hashes.db < check_criminal_minds_import.sql

.mode column
.headers on
.width 40 10

-- Summary Statistics
SELECT '=== CRIMINAL MINDS IMPORT SUMMARY ===' as '';
SELECT '';

SELECT 
    'Total Episodes Imported' as Metric,
    COUNT(*) as Value
FROM SubtitleHashes 
WHERE Series = 'Criminal Minds'
UNION ALL
SELECT 
    'Distinct Seasons',
    COUNT(DISTINCT Season)
FROM SubtitleHashes 
WHERE Series = 'Criminal Minds';

SELECT '';
SELECT '=== BREAKDOWN BY SEASON ===' as '';
SELECT '';

-- Episodes per season
SELECT 
    Season,
    COUNT(*) as Episodes
FROM SubtitleHashes 
WHERE Series = 'Criminal Minds'
GROUP BY Season
ORDER BY CAST(Season AS INTEGER);

SELECT '';
SELECT '=== SAMPLE EPISODES ===' as '';
SELECT '';

-- Show first 10 episodes
SELECT 
    Series,
    'S' || printf('%02d', CAST(Season AS INTEGER)) || 
    'E' || printf('%02d', CAST(Episode AS INTEGER)) as Episode_ID,
    SUBSTR(EpisodeName, 1, 30) as Episode_Name
FROM SubtitleHashes 
WHERE Series = 'Criminal Minds'
ORDER BY CAST(Season AS INTEGER), CAST(Episode AS INTEGER)
LIMIT 10;

SELECT '';
SELECT '=== ALL SERIES IN DATABASE ===' as '';
SELECT '';

-- Show all series with counts
SELECT 
    Series,
    COUNT(*) as Total_Episodes,
    COUNT(DISTINCT Season) as Seasons
FROM SubtitleHashes 
GROUP BY Series
ORDER BY Series;
