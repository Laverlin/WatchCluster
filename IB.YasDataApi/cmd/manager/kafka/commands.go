package kafka


type CommandType int

const (
	AddUser CommandType = iota
)

var (
    commandTypeMap = map[string]CommandType{
        "AddUser":   AddUser,
    }
)
func MapStringCommandType(str string) CommandType {
    c := commandTypeMap[str]
    return c
}



type AddUserCommand struct {
	TelegramId int64
	PublicId string
	UserName string
}

