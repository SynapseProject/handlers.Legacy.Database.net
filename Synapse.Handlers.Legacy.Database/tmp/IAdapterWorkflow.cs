using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sentosa.CommandCenter.Api.Client;

namespace Sentosa.CommandCenter.Adapters.Core
{
	public interface IAdapterWorkflow
	{
		IAdapterWorkflowParameters Parameters { get; set; }
		void ExecuteAction(CommandCenterAction action);

		event EventHandler<AdapterProgressCancelEventArgs> StepStarting;
		event EventHandler<AdapterProgressEventArgs> StepProgress;
		event EventHandler<AdapterProgressEventArgs> StepFinished;
	}

	//marker interface
	public interface IAdapterWorkflowParameters { }

	public abstract class AdapterWorkflowBase : IAdapterWorkflow
	{
		public abstract IAdapterWorkflowParameters Parameters { get; set; }
		public abstract void ExecuteAction(CommandCenterAction action);

		public event EventHandler<AdapterProgressCancelEventArgs> StepStarting;
		public event EventHandler<AdapterProgressEventArgs> StepProgress;
		public event EventHandler<AdapterProgressEventArgs> StepFinished;

		/// <summary>
		/// Notify of step start. If return value is True, then cancel operation.
		/// </summary>
		/// <param name="context">The method name or workflow activty.</param>
		/// <param name="message">Descriptive message.</param>
		/// <param name="status">Overall Package status indicator.</param>
		/// <param name="id">Message Id.</param>
		/// <param name="severity">Message/error severity.</param>
		/// <param name="ex">Current exception (optional).</param>
		/// <returns>AdapterProgressCancelEventArgs.Cancel value.</returns>
		protected virtual bool OnStepStarting(string context, string message, PackageStatus status, int id, int severity, Exception ex)
		{
			AdapterProgressCancelEventArgs e =
				new AdapterProgressCancelEventArgs( context, message, status, id, severity, false, ex );
			OnStepStarting( e );

			return e.Cancel;
		}

		/// <summary>
		/// Notify of step start. If e.Cancel is True, then cancel operation.
		/// </summary>
		protected virtual void OnStepStarting(AdapterProgressCancelEventArgs e)
		{
			if( StepStarting != null )
			{
				StepStarting( this, e );
			}
		}

		/// <summary>
		/// Notify of step progress.
		/// </summary>
		/// <param name="context">The method name or workflow activty.</param>
		/// <param name="message">Descriptive message.</param>
		/// <param name="status">Overall Package status indicator.</param>
		/// <param name="id">Message Id.</param>
		/// <param name="severity">Message/error severity.</param>
		/// <param name="ex">Current exception (optional).</param>
		protected virtual void OnStepProgress(string context, string message, PackageStatus status, int id, int severity, Exception ex)
		{
			if( StepProgress != null )
			{
				StepProgress( this, new AdapterProgressEventArgs( context, message, status, id, severity, ex ) );
			}
		}

		/// <summary>
		/// Notify of step completion.
		/// </summary>
		/// <param name="context">The method name or workflow activty.</param>
		/// <param name="message">Descriptive message.</param>
		/// <param name="status">Overall Package status indicator.</param>
		/// <param name="id">Message Id.</param>
		/// <param name="severity">Message/error severity.</param>
		/// <param name="ex">Current exception (optional).</param>
		protected virtual void OnStepFinished(string context, string message, PackageStatus status, int id, int severity, Exception ex)
		{
			if( StepFinished != null )
			{
				StepFinished( this, new AdapterProgressEventArgs( context, message, status, id, severity, ex ) );
			}
		}
	}
}