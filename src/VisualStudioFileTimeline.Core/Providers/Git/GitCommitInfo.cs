namespace VisualStudioFileTimeline.Providers.Git;

public readonly record struct GitCommitInfo(string CommitId,
                                            string Author,
                                            string AuthorEmail,
                                            long AuthorTimestamp,
                                            string Committer,
                                            string CommitterEmail,
                                            long CommitterTimestamp,
                                            string Body);
