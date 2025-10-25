-- Find Parks and Recreation S04E15 in the database
SELECT Id, Series, Season, Episode, EpisodeName, SubtitleSourceFormat
FROM SubtitleHashes 
WHERE Series LIKE '%Parks and Recreation%' 
  AND Season IN ('4', '04')
  AND Episode IN ('15', '015')
LIMIT 5;
