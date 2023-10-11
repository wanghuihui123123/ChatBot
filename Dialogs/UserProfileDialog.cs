using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChatBot.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;

namespace ChatBot.Dialogs
{
    public class UserProfileDialog : ComponentDialog
    {
        private readonly IStatePropertyAccessor<UserProfile> _userProfileAccessor;

        public UserProfileDialog( UserState userState )
            : base( nameof( UserProfileDialog ) )
        {
            _userProfileAccessor = userState.CreateProperty<UserProfile>( "UserProfile" );

            // This array defines how the Waterfall will execute.
            var waterfallSteps = new WaterfallStep[]
            {
                ArrivalDateStepAsync,
                NumberOfNightsStepAsync,
                SummaryStepAsync,
                FinalStepAsync,
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            AddDialog( new WaterfallDialog( nameof( WaterfallDialog ), waterfallSteps ) );
            AddDialog( new DateTimePrompt( nameof( DateTimePrompt ) ) );
            AddDialog( new NumberPrompt<int>( nameof( NumberPrompt<int> ), NumberOfNightsPromptValidatorAsync ) );
            AddDialog( new TextPrompt( nameof( TextPrompt ) ) );
            AddDialog( new ChoicePrompt( nameof( ChoicePrompt ) ) );
           // AddDialog( new ConfirmPrompt( nameof( ConfirmPrompt ) ) );
          //  AddDialog( new AttachmentPrompt( nameof( AttachmentPrompt ), PicturePromptValidatorAsync ) );

            // The initial child Dialog to run.
            InitialDialogId = nameof( WaterfallDialog );
        }

        private static async Task<DialogTurnResult> ArrivalDateStepAsync( WaterfallStepContext stepContext, CancellationToken cancellationToken )
        {
            return await stepContext.PromptAsync( nameof( DateTimePrompt ), new PromptOptions { Prompt = MessageFactory.Text( "Please enter your arrival date." ) }, cancellationToken );
        }


        private async Task<DialogTurnResult> NumberOfNightsStepAsync( WaterfallStepContext stepContext, CancellationToken cancellationToken )
        {
            stepContext.Values["arrivaldate"] = (( IList<DateTimeResolution> ) stepContext.Result)?.FirstOrDefault()?.Value;
            var promptOptions = new PromptOptions
            {
                Prompt = MessageFactory.Text( "Please enter your number of nights." ),
                RetryPrompt = MessageFactory.Text( "The value entered must be greater than 0." ),
            };

            return await stepContext.PromptAsync( nameof( NumberPrompt<int> ), promptOptions, cancellationToken );
        }


        private async Task<DialogTurnResult> SummaryStepAsync( WaterfallStepContext stepContext, CancellationToken cancellationToken )
        {
            stepContext.Values["numberofnights"] = ( int ) stepContext.Result;

            // Get the current profile object from user state.
            var userProfile = await _userProfileAccessor.GetAsync( stepContext.Context, () => new UserProfile(), cancellationToken );

            userProfile.ArrivalDate = ( string ) stepContext.Values["arrivaldate"];
            userProfile.NumberOfNights = ( int ) stepContext.Values["numberofnights"];


            var msg = $"I have your arrival date is {userProfile.ArrivalDate} and number of nights is {userProfile.NumberOfNights}";
            await stepContext.Context.SendActivityAsync( MessageFactory.Text( msg ), cancellationToken );

            var windSurferAPImsg = "These room types are available for reservation.\r\nKing room,$150-$180 /night\r\nSuite room,$250-$280/night";
            await stepContext.Context.SendActivityAsync( MessageFactory.Text( windSurferAPImsg ), cancellationToken );

            return await stepContext.PromptAsync( nameof( ChoicePrompt ),
             new PromptOptions
             {
                 Prompt = MessageFactory.Text( "which opion do you like?" ),
                 Choices = ChoiceFactory.ToChoices( new List<string> { "King room", "Suite room" } ),
             }, cancellationToken ); 
        }

        private async Task<DialogTurnResult> FinalStepAsync( WaterfallStepContext stepContext, CancellationToken cancellationToken )
        {
           var selectedroomtype = (( FoundChoice ) stepContext.Result).Value;
           
            var reply = stepContext.Context.Activity.CreateReply();
             
            var linkButton = new CardAction
            {
                Type = ActionTypes.OpenUrl,
                Title = "Pleases click this link to complete your reservation",
                Value = "https://uat.windsurfercrs.com/admin"
            };

         
            reply.Attachments = new List<Attachment>
            {
                new HeroCard
                {
                    Buttons = new List<CardAction> { linkButton }
                }.ToAttachment()
            };

            await stepContext.Context.SendActivityAsync( reply, cancellationToken );

            // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is the end.
            return await stepContext.EndDialogAsync( cancellationToken: cancellationToken );
        }
        
        private static Task<bool> NumberOfNightsPromptValidatorAsync( PromptValidatorContext<int> promptContext, CancellationToken cancellationToken )
        {
            // This condition is our validation rule. You can also change the value at this point.
            return Task.FromResult( promptContext.Recognized.Succeeded && promptContext.Recognized.Value > 0 );
        }

    }
}
