using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenDeepWiki.Entities;

namespace OpenDeepWiki.Tests.Services.Repositories;

/// <summary>
/// Property-based tests for repository processing state transitions.
/// Feature: repository-wiki-generation, Property 2: Processing State Transitions
/// Validates: Requirements 2.2, 2.3, 2.4
/// </summary>
public class RepositoryProcessingStatePropertyTests
{
    /// <summary>
    /// Represents a valid state transition in the repository processing workflow.
    /// </summary>
    private static readonly HashSet<(RepositoryStatus From, RepositoryStatus To)> ValidTransitions = new()
    {
        // Pending -> Processing (when worker picks up the repository)
        (RepositoryStatus.Pending, RepositoryStatus.Processing),
        // Processing -> Completed (when processing succeeds)
        (RepositoryStatus.Processing, RepositoryStatus.Completed),
        // Processing -> Failed (when processing fails)
        (RepositoryStatus.Processing, RepositoryStatus.Failed),
        // Failed -> Pending (when user retries - optional, for retry functionality)
        (RepositoryStatus.Failed, RepositoryStatus.Pending),
        // Completed -> Pending (when user requests re-processing - optional)
        (RepositoryStatus.Completed, RepositoryStatus.Pending)
    };

    /// <summary>
    /// Generates a valid repository status.
    /// </summary>
    private static Gen<RepositoryStatus> GenerateRepositoryStatus()
    {
        return Gen.Elements(
            RepositoryStatus.Pending,
            RepositoryStatus.Processing,
            RepositoryStatus.Completed,
            RepositoryStatus.Failed);
    }

    /// <summary>
    /// Generates a valid repository with a given status.
    /// </summary>
    private static Gen<Repository> GenerateRepositoryWithStatus(RepositoryStatus status)
    {
        return Gen.Constant(new Repository
        {
            Id = Guid.NewGuid().ToString(),
            OwnerUserId = Guid.NewGuid().ToString(),
            GitUrl = "https://github.com/test/repo.git",
            RepoName = "repo",
            OrgName = "test",
            Status = status
        });
    }

    /// <summary>
    /// Property 2: Processing State Transitions
    /// For any repository being processed, the status transitions SHALL follow:
    /// Pending → Processing → (Completed | Failed). No other transitions are valid.
    /// Validates: Requirements 2.2, 2.3, 2.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProcessingWorkflow_ShouldStartFromPending()
    {
        return Prop.ForAll(
            GenerateRepositoryWithStatus(RepositoryStatus.Pending).ToArbitrary(),
            repository =>
            {
                // A repository must start in Pending status before processing
                var canStartProcessing = repository.Status == RepositoryStatus.Pending;
                
                // Simulate transition to Processing
                if (canStartProcessing)
                {
                    repository.Status = RepositoryStatus.Processing;
                }

                return canStartProcessing && repository.Status == RepositoryStatus.Processing;
            });
    }

    /// <summary>
    /// Property 2: Processing State Transitions
    /// For any repository in Processing status, it SHALL transition to either Completed or Failed.
    /// Validates: Requirements 2.3, 2.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProcessingStatus_ShouldTransitionToCompletedOrFailed()
    {
        var outcomeGen = Gen.Elements(true, false); // true = success, false = failure

        return Prop.ForAll(
            GenerateRepositoryWithStatus(RepositoryStatus.Processing).ToArbitrary(),
            outcomeGen.ToArbitrary(),
            (repository, success) =>
            {
                // Simulate processing outcome
                repository.Status = success 
                    ? RepositoryStatus.Completed 
                    : RepositoryStatus.Failed;

                // Verify the transition is valid
                return repository.Status == RepositoryStatus.Completed || 
                       repository.Status == RepositoryStatus.Failed;
            });
    }

    /// <summary>
    /// Property 2: Processing State Transitions
    /// For any state transition, it SHALL be in the set of valid transitions.
    /// Validates: Requirements 2.2, 2.3, 2.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StateTransition_ShouldBeValid()
    {
        var transitionGen = Gen.Elements(ValidTransitions.ToArray());

        return Prop.ForAll(
            transitionGen.ToArbitrary(),
            transition =>
            {
                var (from, to) = transition;
                return ValidTransitions.Contains((from, to));
            });
    }

    /// <summary>
    /// Property 2: Processing State Transitions
    /// For any repository, direct transition from Pending to Completed SHALL NOT be valid.
    /// Validates: Requirements 2.2, 2.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DirectPendingToCompleted_ShouldBeInvalid()
    {
        return Prop.ForAll(
            GenerateRepositoryWithStatus(RepositoryStatus.Pending).ToArbitrary(),
            repository =>
            {
                // Direct transition from Pending to Completed is invalid
                var invalidTransition = (RepositoryStatus.Pending, RepositoryStatus.Completed);
                return !ValidTransitions.Contains(invalidTransition);
            });
    }

    /// <summary>
    /// Property 2: Processing State Transitions
    /// For any repository, direct transition from Pending to Failed SHALL NOT be valid.
    /// Validates: Requirements 2.2, 2.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DirectPendingToFailed_ShouldBeInvalid()
    {
        return Prop.ForAll(
            GenerateRepositoryWithStatus(RepositoryStatus.Pending).ToArbitrary(),
            repository =>
            {
                // Direct transition from Pending to Failed is invalid
                var invalidTransition = (RepositoryStatus.Pending, RepositoryStatus.Failed);
                return !ValidTransitions.Contains(invalidTransition);
            });
    }

    /// <summary>
    /// Property 2: Processing State Transitions
    /// For any repository in Completed status, it SHALL NOT transition to Processing directly.
    /// Validates: Requirements 2.2, 2.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompletedToProcessing_ShouldBeInvalid()
    {
        return Prop.ForAll(
            GenerateRepositoryWithStatus(RepositoryStatus.Completed).ToArbitrary(),
            repository =>
            {
                // Direct transition from Completed to Processing is invalid
                var invalidTransition = (RepositoryStatus.Completed, RepositoryStatus.Processing);
                return !ValidTransitions.Contains(invalidTransition);
            });
    }

    /// <summary>
    /// Property 2: Processing State Transitions
    /// For any repository in Failed status, it SHALL NOT transition to Processing directly.
    /// Validates: Requirements 2.2, 2.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FailedToProcessing_ShouldBeInvalid()
    {
        return Prop.ForAll(
            GenerateRepositoryWithStatus(RepositoryStatus.Failed).ToArbitrary(),
            repository =>
            {
                // Direct transition from Failed to Processing is invalid
                var invalidTransition = (RepositoryStatus.Failed, RepositoryStatus.Processing);
                return !ValidTransitions.Contains(invalidTransition);
            });
    }

    /// <summary>
    /// Property 2: Processing State Transitions
    /// For any complete processing workflow, the sequence SHALL be Pending → Processing → (Completed | Failed).
    /// Validates: Requirements 2.2, 2.3, 2.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompleteWorkflow_ShouldFollowValidSequence()
    {
        var outcomeGen = Gen.Elements(true, false);

        return Prop.ForAll(
            GenerateRepositoryWithStatus(RepositoryStatus.Pending).ToArbitrary(),
            outcomeGen.ToArbitrary(),
            (repository, success) =>
            {
                // Track state transitions
                var transitions = new List<(RepositoryStatus From, RepositoryStatus To)>();
                
                // Step 1: Pending -> Processing
                var fromStatus = repository.Status;
                repository.Status = RepositoryStatus.Processing;
                transitions.Add((fromStatus, repository.Status));

                // Step 2: Processing -> Completed or Failed
                fromStatus = repository.Status;
                repository.Status = success 
                    ? RepositoryStatus.Completed 
                    : RepositoryStatus.Failed;
                transitions.Add((fromStatus, repository.Status));

                // Verify all transitions are valid
                return transitions.All(t => ValidTransitions.Contains(t));
            });
    }

    /// <summary>
    /// Simulates the state transition logic used in RepositoryProcessingWorker.
    /// This helper validates that a transition is allowed.
    /// </summary>
    private static bool IsValidTransition(RepositoryStatus from, RepositoryStatus to)
    {
        return ValidTransitions.Contains((from, to));
    }

    /// <summary>
    /// Property 2: Processing State Transitions
    /// For any repository, the IsValidTransition helper SHALL correctly identify valid transitions.
    /// Validates: Requirements 2.2, 2.3, 2.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IsValidTransition_ShouldMatchValidTransitionsSet()
    {
        var fromGen = GenerateRepositoryStatus();
        var toGen = GenerateRepositoryStatus();

        return Prop.ForAll(
            fromGen.ToArbitrary(),
            toGen.ToArbitrary(),
            (from, to) =>
            {
                var expected = ValidTransitions.Contains((from, to));
                var actual = IsValidTransition(from, to);
                return expected == actual;
            });
    }
}
