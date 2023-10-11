// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema; 
using Microsoft.Bot.Builder.Dialogs; 
using Microsoft.Extensions.Logging;

namespace ChatBot.Bots
{
    public class ChatBot<T> : ActivityHandler where T : Dialog
    {
        // Message to send to users when the bot receives a Conversation Update event
        private const string WelcomeMessage1 = "Hi, it's great to see you!";
        private const string WelcomeMessage2 = "What information are you looking for?";
         
        // Dependency injected dictionary for storing ConversationReference objects used in NotifyController to proactively message users
        private readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences;

        protected readonly Dialog Dialog;
        protected readonly BotState ConversationState;
        protected readonly BotState UserState;
        protected readonly ILogger Logger;


        public ChatBot(
            ConversationState conversationState,
            UserState userState,
            T dialog,
            ILogger<ChatBot<T>> logger,
            ConcurrentDictionary<string, ConversationReference> conversationReferences )
        {
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;
            Logger = logger;
            _conversationReferences = conversationReferences;
        }

        public override async Task OnTurnAsync( ITurnContext turnContext, CancellationToken cancellationToken = default )
        {
            await base.OnTurnAsync( turnContext, cancellationToken );

            // Save any state changes that might have occurred during the turn.
            await ConversationState.SaveChangesAsync( turnContext, false, cancellationToken );
            await UserState.SaveChangesAsync( turnContext, false, cancellationToken );
        }

        private void AddConversationReference( Activity activity )
        {
            var conversationReference = activity.GetConversationReference();
            _conversationReferences.AddOrUpdate( conversationReference.User.Id, conversationReference, ( key, newValue ) => conversationReference );
        }

        protected override Task OnConversationUpdateActivityAsync( ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken )
        {
            AddConversationReference( turnContext.Activity as Activity );

            return base.OnConversationUpdateActivityAsync( turnContext, cancellationToken );
        }

        protected override async Task OnMembersAddedAsync( IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken )
        {
            foreach( var member in membersAdded )
            {
                // Greet anyone that was not the target (recipient) of this message.
                if( member.Id != turnContext.Activity.Recipient.Id )
                {
                    await turnContext.SendActivityAsync( MessageFactory.Text( WelcomeMessage1 ), cancellationToken );
                    await turnContext.SendActivityAsync( MessageFactory.Text( WelcomeMessage2 ), cancellationToken );
                }
            }
        }

        protected override async Task OnMessageActivityAsync( ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken )
        {
            AddConversationReference( turnContext.Activity as Activity );
            var activityValue = turnContext.Activity.Value?.ToString();
            if( activityValue == "makehotelreservationyes" )
            {
                // Run the Dialog with the new message Activity.
                await Dialog.RunAsync( turnContext, ConversationState.CreateProperty<DialogState>( nameof( DialogState ) ), cancellationToken );
            }
            else if( activityValue == "makehotelreservationno" )
            {

            }
            else
            {
                var dialogSet = new DialogSet( ConversationState.CreateProperty<DialogState>( nameof( DialogState ) ) );
                var dialogContext = await dialogSet.CreateContextAsync( turnContext, cancellationToken );
                //var dialogResult = await dialogContext.ContinueDialogAsync( cancellationToken );

                if( dialogContext.Stack.Count == 0 )
                {
                    await turnContext.SendActivityAsync( MessageFactory.Text( "openai answer" ), cancellationToken );
                }
                else
                {
                    // Run the Dialog with the new message Activity.
                    await Dialog.RunAsync( turnContext, ConversationState.CreateProperty<DialogState>( nameof( DialogState ) ), cancellationToken );
                }
            } 
        }
    }
}
