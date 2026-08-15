#import "AppSettingsController+SurfaceMode.h"

#import <dispatch/dispatch.h>
#import <objc/runtime.h>

#import "EditButtonTableViewCell.h"
#import "EditSecretTextTableViewCell.h"
#import "EditTextTableViewCell.h"
#import "SurfaceModeControlClient.h"
#import "SurfaceModeSettings.h"
#import "SurfaceModeStatusCenter.h"
#import "Utils.h"

static NSInteger const SurfaceModeSection = 2;
static NSInteger const SurfaceModeRowHost = 0;
static NSInteger const SurfaceModeRowPort = 1;
static NSInteger const SurfaceModeRowToken = 2;
static NSInteger const SurfaceModeRowTest = 3;
static NSInteger const SurfaceModeRowStatus = 4;

@implementation AppSettingsController (SurfaceMode)

+ (void)load
{
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        [self swizzleSelector:@selector(viewDidLoad) with:@selector(sm_viewDidLoad)];
        [self swizzleSelector:@selector(dealloc) with:@selector(sm_dealloc)];
        [self swizzleSelector:@selector(numberOfSectionsInTableView:) with:@selector(sm_numberOfSectionsInTableView:)];
        [self swizzleSelector:@selector(tableView:numberOfRowsInSection:) with:@selector(sm_tableView:numberOfRowsInSection:)];
        [self swizzleSelector:@selector(tableView:titleForHeaderInSection:) with:@selector(sm_tableView:titleForHeaderInSection:)];
        [self swizzleSelector:@selector(tableView:cellForRowAtIndexPath:) with:@selector(sm_tableView:cellForRowAtIndexPath:)];
        [self swizzleSelector:@selector(tableView:didSelectRowAtIndexPath:) with:@selector(sm_tableView:didSelectRowAtIndexPath:)];
    });
}

+ (void)swizzleSelector:(SEL)original with:(SEL)replacement
{
    Method originalMethod = class_getInstanceMethod(self, original);
    Method replacementMethod = class_getInstanceMethod(self, replacement);
    method_exchangeImplementations(originalMethod, replacementMethod);
}

- (void)sm_viewDidLoad
{
    [self sm_viewDidLoad];
    [[NSNotificationCenter defaultCenter] addObserver:self
                                             selector:@selector(surfaceModeStatusDidChange:)
                                                 name:SurfaceModeStatusDidChangeNotification
                                               object:[SurfaceModeStatusCenter sharedCenter]];
}

- (void)sm_dealloc
{
    [[NSNotificationCenter defaultCenter] removeObserver:self];
    [self sm_dealloc];
}

- (NSInteger)sm_numberOfSectionsInTableView:(UITableView *)tableView
{
    return [self sm_numberOfSectionsInTableView:tableView] + 1;
}

- (NSInteger)sm_tableView:(UITableView *)tableView numberOfRowsInSection:(NSInteger)section
{
    if (section == SurfaceModeSection)
    {
        return 5;
    }
    return [self sm_tableView:tableView numberOfRowsInSection:section];
}

- (NSString *)sm_tableView:(UITableView *)tableView titleForHeaderInSection:(NSInteger)section
{
    if (section == SurfaceModeSection)
    {
        return @"Surface Mode Control";
    }
    return [self sm_tableView:tableView titleForHeaderInSection:section];
}

- (UITableViewCell *)sm_tableView:(UITableView *)tableView cellForRowAtIndexPath:(NSIndexPath *)indexPath
{
    if (indexPath.section != SurfaceModeSection)
    {
        return [self sm_tableView:tableView cellForRowAtIndexPath:indexPath];
    }

    if (indexPath.row == SurfaceModeRowStatus)
    {
        static NSString *identifier = @"SurfaceModeStatusCell";
        UITableViewCell *cell = [tableView dequeueReusableCellWithIdentifier:identifier];
        if (cell == nil)
        {
            cell = [[[UITableViewCell alloc] initWithStyle:UITableViewCellStyleSubtitle
                                           reuseIdentifier:identifier] autorelease];
            cell.selectionStyle = UITableViewCellSelectionStyleNone;
        }

        SurfaceModeStatusCenter *status = [SurfaceModeStatusCenter sharedCenter];
        cell.textLabel.text = status.statusText;
        cell.detailTextLabel.text = status.detailText;
        cell.accessoryType = UITableViewCellAccessoryNone;
        return cell;
    }

    NSString *identifier = TableCellIdentifierText;
    if (indexPath.row == SurfaceModeRowToken)
    {
        identifier = TableCellIdentifierSecretText;
    }
    else if (indexPath.row == SurfaceModeRowTest)
    {
        identifier = TableCellIdentifierButton;
    }

    UITableViewCell *cell = [self tableViewCellFromIdentifier:identifier];
    if (indexPath.row == SurfaceModeRowTest)
    {
        EditButtonTableViewCell *buttonCell = (EditButtonTableViewCell *)cell;
        buttonCell.label.text = @"Test Connection";
        [buttonCell.button setTitle:@"Send PING" forState:UIControlStateNormal];
        [buttonCell.button removeTarget:nil action:NULL forControlEvents:UIControlEventAllEvents];
        [buttonCell.button addTarget:self
                              action:@selector(surfaceModeTestConnectionTapped:)
                    forControlEvents:UIControlEventTouchUpInside];
        return cell;
    }

    EditTextTableViewCell *textCell = nil;
    if (indexPath.row == SurfaceModeRowToken)
    {
        textCell = (EditSecretTextTableViewCell *)cell;
        ((EditSecretTextTableViewCell *)textCell).textfield.secureTextEntry = YES;
    }
    else
    {
        textCell = (EditTextTableViewCell *)cell;
    }

    textCell.textfield.delegate = self;
    textCell.textfield.tag = GET_TAG(SurfaceModeSection, indexPath.row);

    if (indexPath.row == SurfaceModeRowHost)
    {
        textCell.label.text = @"Windows VM Hostname / IP";
        textCell.textfield.placeholder = @"100.x.x.x or hostname";
        textCell.textfield.text = [SurfaceModeSettings host];
        textCell.textfield.autocapitalizationType = UITextAutocapitalizationTypeNone;
        textCell.textfield.autocorrectionType = UITextAutocorrectionTypeNo;
    }
    else if (indexPath.row == SurfaceModeRowPort)
    {
        textCell.label.text = @"Control Port";
        textCell.textfield.keyboardType = UIKeyboardTypeNumberPad;
        textCell.textfield.placeholder = @"47889";
        textCell.textfield.text = [NSString stringWithFormat:@"%ld", (long)[SurfaceModeSettings port]];
    }
    else if (indexPath.row == SurfaceModeRowToken)
    {
        EditSecretTextTableViewCell *secretCell = (EditSecretTextTableViewCell *)cell;
        secretCell.label.text = @"Authentication Token";
        secretCell.textfield.placeholder = @"Stored in Keychain";
        secretCell.textfield.text = [SurfaceModeSettings token];
    }

    [self adjustEditTextTableViewCell:(EditTextTableViewCell *)cell];
    return cell;
}

- (void)sm_tableView:(UITableView *)tableView didSelectRowAtIndexPath:(NSIndexPath *)indexPath
{
    if (indexPath.section == SurfaceModeSection)
    {
        [tableView deselectRowAtIndexPath:indexPath animated:YES];
        [self.view endEditing:YES];
        if (indexPath.row == SurfaceModeRowTest)
        {
            [self surfaceModeTestConnectionTapped:nil];
        }
        return;
    }

    [self sm_tableView:tableView didSelectRowAtIndexPath:indexPath];
}

- (void)surfaceModeStatusDidChange:(NSNotification *)notification
{
    (void)notification;
    dispatch_async(dispatch_get_main_queue(), ^{
        NSIndexSet *sections = [NSIndexSet indexSetWithIndex:SurfaceModeSection];
        [self.tableView reloadSections:sections withRowAnimation:UITableViewRowAnimationNone];
    });
}

- (void)surfaceModeTestConnectionTapped:(id)sender
{
    (void)sender;
    [self.view endEditing:YES];
    [[SurfaceModeControlClient sharedClient] testConnection];
}

- (void)textFieldDidEndEditing:(UITextField *)textField
{
    switch (textField.tag)
    {
        case GET_TAG(SurfaceModeSection, SurfaceModeRowHost):
            [SurfaceModeSettings setHost:textField.text];
            break;
        case GET_TAG(SurfaceModeSection, SurfaceModeRowPort):
            [SurfaceModeSettings setPort:[textField.text integerValue]];
            break;
        case GET_TAG(SurfaceModeSection, SurfaceModeRowToken):
            [SurfaceModeSettings setToken:textField.text];
            break;
        default:
            break;
    }
}

- (BOOL)textFieldShouldReturn:(UITextField *)textField
{
    [textField resignFirstResponder];
    return YES;
}

@end
