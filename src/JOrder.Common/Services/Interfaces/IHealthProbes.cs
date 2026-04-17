namespace JOrder.Common.Services.Interfaces;

public interface IHealthProbes
{
    Task StartupProbe();
    Task LivenessProbe();
    Task ReadinessProbe();
}